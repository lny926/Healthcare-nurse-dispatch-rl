import copy
import random

from .config_loader import load_unity_settings
from .sim_core import CareSimulation


class NurseDispatchEnv:
    def __init__(self, config=None):
        self.base_config = config if config is not None else load_unity_settings()
        self.config = None
        self.sim = None

        self.max_steps = 500
        self.current_step = 0

        self.action_space_size = 5  # 策略
        self.observation_size = 20

    def reset(self, seed=1):
        self.config = copy.deepcopy(self.base_config)
        self.config["experiment"]["random_seed"] = seed
        self.config["experiment"]["dispatch_mode"] = "rl"

        random.seed(seed)

        self.sim = CareSimulation(self.config)

        self.current_step = 0

        return self.get_observation()

    def step(self, action):
        """
        action: int, 0-5, 表示选择哪一个护士
        """

        self.current_step += 1

        reward_before = self.get_reward_signal()

        # 先推进 simulation 一小段时间
        # 这里先用简化版：每 step 推进 60 秒
        for _ in range(60):
            self.sim.spawn_tasks_if_needed()
            self.sim.spawn_routine_tasks_if_needed()
            self.sim.update_waiting_tasks()
            self.sim.complete_finished_nurse_tasks()
            self.sim.update_resting_state()

            self.apply_rl_action(action)

            self.sim.recover_fatigue()
            self.sim.update_shift()
            self.sim.record_fatigue_sample()

            self.sim.time += 1.0
            self.sim.elapsed_seconds += 1.0

            if self.sim.elapsed_seconds >= self.sim.end_elapsed_seconds:
                break

        reward_after = self.get_reward_signal()

        reward = reward_after - reward_before

        done = (
            self.sim.elapsed_seconds >= self.sim.end_elapsed_seconds
            or self.current_step >= self.max_steps
        )

        observation = self.get_observation()
        info = self.sim.get_metrics() if done else {}

        return observation, reward, done, info

    def apply_rl_action(self, action):
        if not self.sim.pending_tasks:
            return

        available_nurses = [
            n for n in self.sim.nurses
            if n.available_at <= self.sim.elapsed_seconds
               and n.active_task is None
               and n.active_routine_task is None
               and not n.is_resting
               and n.fatigue < 1.0
        ]

        if not available_nurses:
            return

        task = None
        nurse = None

        # action 0: shortest distance
        if action == 0:
            best_pair = None
            best_distance = float("inf")

            for n in available_nurses:
                for t in self.sim.pending_tasks:
                    dist = self.sim.distance(n.position_room, t.room_id)
                    if dist < best_distance:
                        best_distance = dist
                        best_pair = (n, t)

            if best_pair is not None:
                nurse, task = best_pair

        # action 1: priority first
        elif action == 1:
            task = max(
                self.sim.pending_tasks,
                key=lambda t: self.sim.task_priority(t.task_type)
            )

            nurse = min(
                available_nurses,
                key=lambda n: self.sim.distance(n.position_room, task.room_id)
            )

        # action 2: lowest fatigue nurse
        elif action == 2:
            nurse = min(
                available_nurses,
                key=lambda n: n.fatigue
            )

            task = max(
                self.sim.pending_tasks,
                key=lambda t: t.waiting_seconds
            )

        # action 3: FCFS
        elif action == 3:
            task = self.sim.pending_tasks[0]

            nurse = min(
                available_nurses,
                key=lambda n: self.sim.distance(n.position_room, task.room_id)
            )

        # action 4: AI score
        elif action == 4:
            best_pair = None
            best_score = float("inf")

            for n in available_nurses:
                for t in self.sim.pending_tasks:
                    score = self.sim.ai_score(n, t)
                    if score < best_score:
                        best_score = score
                        best_pair = (n, t)

            if best_pair is not None:
                nurse, task = best_pair

        if nurse is None or task is None:
            return

        self.sim.assign_normal_task(nurse, task)
        self.sim.pending_tasks.remove(task)

    def get_observation(self):
        obs = []

        # 1. nurse fatigue, 6 values
        for nurse in self.sim.nurses:
            obs.append(nurse.fatigue)

        # 2. nurse availability, 6 values
        for nurse in self.sim.nurses:
            available = (
                nurse.available_at <= self.sim.elapsed_seconds
                and nurse.active_task is None
                and nurse.active_routine_task is None
                and not nurse.is_resting
            )
            obs.append(1.0 if available else 0.0)

        # 3. pending task count
        obs.append(min(len(self.sim.pending_tasks) / 12.0, 1.0))

        # 4. routine task count
        obs.append(min(len(self.sim.pending_routine_tasks) / 12.0, 1.0))

        # 5. average waiting time of pending tasks
        if self.sim.pending_tasks:
            avg_wait = sum(t.waiting_seconds for t in self.sim.pending_tasks) / len(self.sim.pending_tasks)
        else:
            avg_wait = 0.0

        obs.append(min(avg_wait / 3600.0, 1.0))

        # 6. max waiting time
        if self.sim.pending_tasks:
            max_wait = max(t.waiting_seconds for t in self.sim.pending_tasks)
        else:
            max_wait = 0.0

        obs.append(min(max_wait / 3600.0, 1.0))

        # 7. task type ratios
        light = sum(1 for t in self.sim.pending_tasks if t.task_type == "light")
        medium = sum(1 for t in self.sim.pending_tasks if t.task_type == "medium")
        heavy = sum(1 for t in self.sim.pending_tasks if t.task_type == "heavy")
        total = max(len(self.sim.pending_tasks), 1)

        obs.append(light / total)
        obs.append(medium / total)
        obs.append(heavy / total)

        # 8. time progress
        obs.append(self.sim.elapsed_seconds / self.sim.end_elapsed_seconds)

        # 保证长度固定为 20
        while len(obs) < self.observation_size:
            obs.append(0.0)

        return obs[:self.observation_size]

    def get_reward_signal(self):
        metrics = self.sim.get_metrics()

        completed = metrics["completed_tasks"]
        completion_rate = metrics["completion_rate"]

        avg_wait_min = metrics["average_waiting_time"] / 60.0
        p95_wait_min = metrics["p95_waiting_time"] / 60.0

        escalations = metrics["escalations"]
        heavy_secondary = metrics["heavy_secondary"]

        total_distance = metrics["total_distance"]
        avg_fatigue = metrics["average_fatigue"]

        pending_tasks = metrics["pending_tasks_left"]
        pending_routine = metrics["pending_routine_tasks_left"]

        reward = 0.0

        # Stronger completion incentive
        reward += completed * 12.0
        reward += completion_rate * 80.0

        # Moderate waiting penalty
        reward -= avg_wait_min * 2.0
        reward -= p95_wait_min * 1.0

        # Moderate escalation penalty
        reward -= escalations * 4.0
        reward -= heavy_secondary * 6.0

        # Distance penalty
        reward -= total_distance / 1000.0

        # Softer fatigue penalty
        reward -= avg_fatigue * 12.0

        # Softer unfinished task penalty
        reward -= pending_tasks * 4.0
        reward -= pending_routine * 2.0

        return reward


if __name__ == "__main__":
    env = NurseDispatchEnv()

    obs = env.reset(seed=1)

    print("Initial observation:")
    print(obs)
    print("Observation size:", len(obs))

    done = False
    total_reward = 0

    while not done:
        action = random.randint(0, env.action_space_size - 1)
        obs, reward, done, info = env.step(action)
        total_reward += reward

    print("\nRandom policy test finished.")
    print("Total reward:", total_reward)
    print("Final info:")
    print(info)