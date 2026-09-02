import random
import math
import heapq
import statistics
from dataclasses import dataclass
from typing import Optional


@dataclass
class Task:
    room_id: str
    task_type: str
    created_time: float
    duration_seconds: float
    waiting_seconds: float = 0.0
    assigned_waiting_seconds: float = 0.0
    escalation_threshold_seconds: float = 0.0
    heavy_secondary_triggered: bool = False
    has_escalated: bool = False


@dataclass
class RoutineTask:
    room_id: str
    created_time: float
    duration_seconds: float
    is_being_handled: bool = False


@dataclass
class Nurse:
    nurse_id: int
    position_room: Optional[str] = None
    station_point: Optional[str] = None
    exit_point: Optional[str] = None
    available_at: float = 0.0
    fatigue: float = 0.0
    worked_seconds: float = 0.0
    completed_tasks: int = 0
    completed_routine_tasks: int = 0
    total_distance: float = 0.0

    active_task: Optional[Task] = None
    active_routine_task: Optional[RoutineTask] = None
    is_resting: bool = False


class CareSimulation:
    def __init__(self, config):
        self.config = config

        exp = config["experiment"]
        scene = config["scene"]

        random.seed(exp["random_seed"])

        self.start_seconds = exp["start_hour"] * 3600 + exp["start_minute"] * 60
        self.time = self.start_seconds
        self.elapsed_seconds = 0.0
        self.end_elapsed_seconds = exp["duration_hours"] * 3600

        self.room_ids = scene["room_ids"]

        self.nurses = [
            Nurse(
                nurse_id=i + 1,
                position_room=f"Nurse{i + 1}Station",
                station_point=f"Nurse{i + 1}Station",
                exit_point=f"Nurse{i + 1}Exit"
            )
            for i in range(scene["nurse_count"])
        ]

        self.pending_tasks = []
        self.pending_routine_tasks = []

        self.next_spawn_elapsed = 0.0
        self.next_routine_elapsed = config["routine_tasks"]["routine_interval_seconds"]

        self.total_tasks_created = 0
        self.completed_tasks = 0
        self.routine_created = 0
        self.routine_completed = 0

        self.total_waiting_time = 0.0
        self.completed_waiting_times = []
        self.max_waiting_time = 0.0

        self.escalation_count = 0
        self.light_to_medium = 0
        self.medium_to_heavy = 0
        self.heavy_secondary = 0

        self.total_distance = 0.0
        self.fatigue_sum_over_time = 0.0
        self.fatigue_sample_count = 0

        self.schedule_next_spawn()

    def run(self):
        while self.elapsed_seconds < self.end_elapsed_seconds:
            self.spawn_tasks_if_needed()
            self.spawn_routine_tasks_if_needed()
            self.update_waiting_tasks()
            self.complete_finished_nurse_tasks()
            self.update_resting_state()
            self.dispatch_tasks()
            self.recover_fatigue()
            self.update_shift()
            self.record_fatigue_sample()

            self.time += 1.0
            self.elapsed_seconds += 1.0

        return self.get_metrics()

    def record_fatigue_sample(self):
        if not self.nurses:
            return

        current_avg = sum(n.fatigue for n in self.nurses) / len(self.nurses)
        self.fatigue_sum_over_time += current_avg
        self.fatigue_sample_count += 1

    def get_current_hour(self):
        seconds_in_day = self.time % 86400
        return int(seconds_in_day // 3600)

    def get_current_window(self):
        tg = self.config["task_generation"]
        hour = self.get_current_hour()

        for key in ["morning", "midday", "evening"]:
            window = tg[key]
            if window["start_hour"] <= hour < window["end_hour"]:
                return window

        return tg["normal"]

    def schedule_next_spawn(self):
        window = self.get_current_window()
        self.next_spawn_elapsed = self.elapsed_seconds + random.uniform(
            window["min_spawn_interval_seconds"],
            window["max_spawn_interval_seconds"]
        )

    def spawn_tasks_if_needed(self):
        if not self.config["task_generation"]["auto_generate"]:
            return

        if self.elapsed_seconds < self.next_spawn_elapsed:
            return

        window = self.get_current_window()
        task_count = self.sample_task_count(window)

        for _ in range(task_count):
            self.try_generate_task(window)

        self.schedule_next_spawn()

    def try_generate_task(self, window):
        available_rooms = [
            r for r in self.room_ids
            if not self.room_has_normal_task(r)
        ]

        if not available_rooms:
            return

        room_id = random.choice(available_rooms)
        task_type = self.sample_task_type(window)

        task = self.create_task(room_id, task_type)

        self.pending_tasks.append(task)
        self.total_tasks_created += 1

    def room_has_normal_task(self, room_id):
        if any(t.room_id == room_id for t in self.pending_tasks):
            return True

        for nurse in self.nurses:
            if nurse.active_task is not None and nurse.active_task.room_id == room_id:
                return True

        return False

    def sample_task_type(self, window):
        weights = window["task_type_weights"]

        total = weights["light"] + weights["medium"] + weights["heavy"]
        value = random.randint(0, total - 1)

        if value < weights["light"]:
            return "light"

        if value < weights["light"] + weights["medium"]:
            return "medium"

        return "heavy"

    def sample_task_count(self, window):
        weights = window["task_count_weights_per_burst"]

        options = [
            (1, weights["one"]),
            (2, weights["two"]),
            (3, weights["three"]),
            (4, weights["four"])
        ]

        total = sum(w for _, w in options)
        value = random.randint(0, total - 1)

        current = 0

        for count, weight in options:
            current += weight
            if value < current:
                return count

        return 1

    def create_task(self, room_id, task_type):
        return Task(
            room_id=room_id,
            task_type=task_type,
            created_time=self.elapsed_seconds,
            duration_seconds=self.sample_task_duration(task_type),
            escalation_threshold_seconds=self.sample_escalation_threshold()
        )

    def sample_task_duration(self, task_type):
        if task_type == "light":
            return random.uniform(2, 5) * 60

        if task_type == "medium":
            return random.uniform(6, 15) * 60

        if task_type == "heavy":
            return random.uniform(12, 30) * 60

        return 5 * 60

    def sample_escalation_threshold(self):
        esc = self.config["escalation"]

        return random.uniform(
            esc["min_escalation_minutes"],
            esc["max_escalation_minutes"]
        ) * 60

    def spawn_routine_tasks_if_needed(self):
        routine = self.config["routine_tasks"]

        if not routine["enabled"]:
            return

        while self.elapsed_seconds >= self.next_routine_elapsed:
            self.trigger_routine_tasks()
            self.next_routine_elapsed += routine["routine_interval_seconds"]

    def trigger_routine_tasks(self):
        routine = self.config["routine_tasks"]

        available_rooms = [
            r for r in self.room_ids
            if not self.room_has_routine_task(r)
        ]

        random.shuffle(available_rooms)

        count = min(routine["rooms_per_trigger"], len(available_rooms))

        for i in range(count):
            room_id = available_rooms[i]

            duration = random.uniform(
                routine["medication_duration_min_minutes"],
                routine["medication_duration_max_minutes"]
            ) * 60

            self.pending_routine_tasks.append(
                RoutineTask(
                    room_id=room_id,
                    created_time=self.elapsed_seconds,
                    duration_seconds=duration
                )
            )

            self.routine_created += 1

    def room_has_routine_task(self, room_id):
        if any(t.room_id == room_id for t in self.pending_routine_tasks):
            return True

        for nurse in self.nurses:
            if nurse.active_routine_task is not None and nurse.active_routine_task.room_id == room_id:
                return True

        return False

    def update_waiting_tasks(self):
        for task in self.pending_tasks:
            task.waiting_seconds = self.elapsed_seconds - task.created_time

            if task.waiting_seconds < task.escalation_threshold_seconds:
                continue

            if task.task_type == "light":
                task.task_type = "medium"
                task.duration_seconds = self.sample_task_duration("medium")
                task.escalation_threshold_seconds = self.sample_escalation_threshold()
                task.created_time = self.elapsed_seconds
                task.waiting_seconds = 0.0

                self.escalation_count += 1
                self.light_to_medium += 1

            elif task.task_type == "medium":
                task.task_type = "heavy"
                task.duration_seconds = self.sample_task_duration("heavy")
                task.escalation_threshold_seconds = self.sample_escalation_threshold()
                task.created_time = self.elapsed_seconds
                task.waiting_seconds = 0.0

                self.escalation_count += 1
                self.medium_to_heavy += 1

            elif task.task_type == "heavy":
                if not task.heavy_secondary_triggered:
                    task.heavy_secondary_triggered = True

                    self.escalation_count += 1
                    self.heavy_secondary += 1

                task.waiting_seconds = task.escalation_threshold_seconds

    def complete_finished_nurse_tasks(self):
        for nurse in self.nurses:
            if nurse.available_at > self.elapsed_seconds:
                continue

            if nurse.active_task is not None:
                task = nurse.active_task

                nurse.completed_tasks += 1
                self.completed_tasks += 1

                final_wait = task.assigned_waiting_seconds

                self.total_waiting_time += final_wait
                self.completed_waiting_times.append(final_wait)
                self.max_waiting_time = max(self.max_waiting_time, final_wait)

                if task.task_type == "heavy":
                    nurse.fatigue += 0.08
                    nurse.fatigue = min(nurse.fatigue, 1.0)

                nurse.position_room = task.room_id
                nurse.active_task = None

                if nurse.fatigue >= 1.0:
                    nurse.is_resting = True
                    nurse.position_room = None

            if nurse.active_routine_task is not None:
                task = nurse.active_routine_task

                nurse.completed_routine_tasks += 1
                self.routine_completed += 1

                nurse.position_room = task.room_id
                nurse.active_routine_task = None

    def update_resting_state(self):
        for nurse in self.nurses:
            if nurse.is_resting and nurse.fatigue <= 0.5:
                nurse.is_resting = False

    def dispatch_tasks(self):
        while True:
            available_nurses = [
                n for n in self.nurses
                if n.available_at <= self.elapsed_seconds
                and n.active_task is None
                and n.active_routine_task is None
                and not n.is_resting
                and n.fatigue < 1.0
            ]

            if not available_nurses:
                break

            if self.pending_tasks:
                nurse, task = self.select_normal_pair(available_nurses)

                if nurse is None or task is None:
                    break

                self.assign_normal_task(nurse, task)
                self.pending_tasks.remove(task)

                continue

            if self.pending_routine_tasks:
                before_count = len(self.pending_routine_tasks)

                self.dispatch_routine_task(available_nurses)

                after_count = len(self.pending_routine_tasks)

                if before_count == after_count:
                    break

                continue

            break

    def select_normal_pair(self, available_nurses):
        mode = self.config["experiment"]["dispatch_mode"]

        if mode == "fcfs":
            return available_nurses[0], self.pending_tasks[0]

        if mode == "priority_first":
            task = max(
                self.pending_tasks,
                key=lambda t: self.task_priority(t.task_type)
            )

            return available_nurses[0], task

        if mode == "shortest_distance":
            best = None
            best_distance = float("inf")

            for nurse in available_nurses:
                for task in self.pending_tasks:
                    dist = self.distance(nurse.position_room, task.room_id)

                    if dist < best_distance:
                        best_distance = dist
                        best = (nurse, task)

            return best

        if mode == "ai_score":
            best = None
            best_score = float("inf")

            for nurse in available_nurses:
                for task in self.pending_tasks:
                    score = self.ai_score(nurse, task)

                    if score < best_score:
                        best_score = score
                        best = (nurse, task)

            return best

        return available_nurses[0], self.pending_tasks[0]

    def dispatch_routine_task(self, available_nurses):
        nurse = available_nurses[0]

        for task in list(self.pending_routine_tasks):
            if self.config["routine_tasks"]["blocked_by_normal_task"]:
                if self.room_has_normal_task(task.room_id):
                    continue

            self.assign_routine_task(nurse, task)
            self.pending_routine_tasks.remove(task)
            return

    def assign_normal_task(self, nurse, task):
        task.assigned_waiting_seconds = self.elapsed_seconds - task.created_time

        dist = self.distance(nurse.position_room, task.room_id)

        move_speed = self.current_move_speed(nurse)
        travel_seconds = (dist / move_speed) * 60 if move_speed > 0 else 0

        nurse.available_at = self.elapsed_seconds + travel_seconds + task.duration_seconds
        nurse.active_task = task

        nurse.worked_seconds += travel_seconds + task.duration_seconds
        nurse.total_distance += dist
        self.total_distance += dist

    def assign_routine_task(self, nurse, task):
        dist = self.distance(nurse.position_room, task.room_id)

        move_speed = self.current_move_speed(nurse)
        travel_seconds = (dist / move_speed) * 60 if move_speed > 0 else 0

        nurse.available_at = self.elapsed_seconds + travel_seconds + task.duration_seconds
        nurse.active_routine_task = task

        nurse.worked_seconds += travel_seconds + task.duration_seconds
        nurse.total_distance += dist
        self.total_distance += dist

    def recover_fatigue(self):
        for nurse in self.nurses:
            if nurse.available_at <= self.elapsed_seconds:
                nurse.fatigue -= 0.002 / 60
                nurse.fatigue = max(nurse.fatigue, 0.0)

    def update_shift(self):
        shift_seconds = self.config["nurse"]["shift_length_hours"] * 3600

        current_shift_index = int(self.elapsed_seconds // shift_seconds)

        if not hasattr(self, "last_shift_index"):
            self.last_shift_index = current_shift_index
            return

        if current_shift_index > self.last_shift_index:
            for nurse in self.nurses:
                if nurse.available_at <= self.elapsed_seconds:
                    nurse.fatigue = 0.0
                    nurse.position_room = nurse.station_point
                    nurse.is_resting = False

            self.last_shift_index = current_shift_index

    def current_move_speed(self, nurse):
        base_speed = self.config["nurse"]["move_speed"]

        multiplier = 1.0 - nurse.fatigue * 0.5
        multiplier = max(0.2, min(1.0, multiplier))

        return base_speed * multiplier

    def ai_score(self, nurse, task):
        weights = self.config["dispatch"]["ai_score_weights"]

        return (
            weights["distance_weight"] * self.distance(nurse.position_room, task.room_id)
            + weights["fatigue_weight"] * nurse.fatigue
            - weights["priority_weight"] * self.task_priority(task.task_type)
            - weights["waiting_time_weight"] * task.waiting_seconds
        )

    def task_priority(self, task_type):
        return self.config["dispatch"]["priority_values"][task_type]

    def distance(self, from_room, to_room):
        points = {
            "BaseCenter": (2.85726, -9.860849),

            "LeftBaseCenter": (-15.14274, -9.860849),
            "LeftBotPoint": (-15.14274, 3.139151),
            "LeftMidPoint": (-15.14274, 15.13915),
            "LeftTopPoint": (-15.14274, 27.13915),

            "RightBaseCenter": (20.85726, -9.860849),
            "RightBotPoint": (20.85726, 3.139151),
            "RightMidPoint": (20.85726, 15.13915),
            "RightTopPoint": (20.85726, 27.13915),

            "A1": (-21.75, 12),
            "A2": (-14.25, 12),
            "A3": (-21.75, 0),
            "A4": (-14.25, 0),
            "A5": (-21.75, -12),
            "A6": (-14.25, -12),

            "B1": (14.25, 12),
            "B2": (21.75, 12),
            "B3": (14.25, 0),
            "B4": (21.75, 0),
            "B5": (14.25, -12),
            "B6": (21.75, -12),

            "Nurse1Station": (-12, -1.3),
            "Nurse1Exit": (-12, -5.860849),

            "Nurse2Station": (-10, -1.3),
            "Nurse2Exit": (-10, -5.860849),

            "Nurse3Station": (-8, -1.3),
            "Nurse3Exit": (-8, -5.860849),

            "Nurse4Station": (-6, -1.3),
            "Nurse4Exit": (-6, -5.860849),

            "Nurse5Station": (-4, -1.3),
            "Nurse5Exit": (-4, -5.860849),

            "Nurse6Station": (-2, -1.3),
            "Nurse6Exit": (-2, -5.860849),
        }

        edges = [
            ("BaseCenter", "LeftBaseCenter"),
            ("LeftBaseCenter", "LeftBotPoint"),
            ("LeftBotPoint", "LeftMidPoint"),
            ("LeftMidPoint", "LeftTopPoint"),

            ("BaseCenter", "RightBaseCenter"),
            ("RightBaseCenter", "RightBotPoint"),
            ("RightBotPoint", "RightMidPoint"),
            ("RightMidPoint", "RightTopPoint"),

            ("LeftTopPoint", "A1"),
            ("LeftTopPoint", "A2"),
            ("LeftMidPoint", "A3"),
            ("LeftMidPoint", "A4"),
            ("LeftBotPoint", "A5"),
            ("LeftBotPoint", "A6"),

            ("RightTopPoint", "B1"),
            ("RightTopPoint", "B2"),
            ("RightMidPoint", "B3"),
            ("RightMidPoint", "B4"),
            ("RightBotPoint", "B5"),
            ("RightBotPoint", "B6"),

            ("Nurse1Station", "Nurse1Exit"),
            ("Nurse1Exit", "BaseCenter"),

            ("Nurse2Station", "Nurse2Exit"),
            ("Nurse2Exit", "BaseCenter"),

            ("Nurse3Station", "Nurse3Exit"),
            ("Nurse3Exit", "BaseCenter"),

            ("Nurse4Station", "Nurse4Exit"),
            ("Nurse4Exit", "BaseCenter"),

            ("Nurse5Station", "Nurse5Exit"),
            ("Nurse5Exit", "BaseCenter"),

            ("Nurse6Station", "Nurse6Exit"),
            ("Nurse6Exit", "BaseCenter"),
        ]

        graph = {name: [] for name in points}

        for a, b in edges:
            ax, ay = points[a]
            bx, by = points[b]
            d = math.sqrt((ax - bx) ** 2 + (ay - by) ** 2)

            graph[a].append((b, d))
            graph[b].append((a, d))

        start = "BaseCenter" if from_room is None else from_room
        goal = to_room

        queue = [(0, start)]
        visited = set()

        while queue:
            current_dist, node = heapq.heappop(queue)

            if node == goal:
                distance_scale = 1.0
                return current_dist * distance_scale

            if node in visited:
                continue

            visited.add(node)

            for neighbor, edge_dist in graph[node]:
                if neighbor not in visited:
                    heapq.heappush(queue, (current_dist + edge_dist, neighbor))

        return 0

    def get_metrics(self):
        avg_wait = (
            self.total_waiting_time / self.completed_tasks
            if self.completed_tasks > 0 else 0
        )

        sorted_waits = sorted(self.completed_waiting_times)

        if sorted_waits:
            p95_index = int(len(sorted_waits) * 0.95) - 1
            p95_index = max(0, min(p95_index, len(sorted_waits) - 1))
            p95_wait = sorted_waits[p95_index]
        else:
            p95_wait = 0

        total_generated = self.total_tasks_created + self.routine_created
        total_completed = self.completed_tasks + self.routine_completed

        completion_rate = (
            total_completed / total_generated
            if total_generated > 0 else 0
        )

        normal_workloads = [n.completed_tasks for n in self.nurses]
        routine_workloads = [n.completed_routine_tasks for n in self.nurses]
        total_workloads = [
            n.completed_tasks + n.completed_routine_tasks
            for n in self.nurses
        ]

        nurse_distances = [n.total_distance for n in self.nurses]
        nurse_final_fatigues = [n.fatigue for n in self.nurses]

        workload_std = statistics.stdev(total_workloads) if len(total_workloads) > 1 else 0.0
        normal_workload_std = statistics.stdev(normal_workloads) if len(normal_workloads) > 1 else 0.0
        distance_std = statistics.stdev(nurse_distances) if len(nurse_distances) > 1 else 0.0
        final_fatigue_std = statistics.stdev(nurse_final_fatigues) if len(nurse_final_fatigues) > 1 else 0.0
        final_fatigue = sum(n.fatigue for n in self.nurses) / len(self.nurses)

        avg_fatigue = (
            self.fatigue_sum_over_time / self.fatigue_sample_count
            if self.fatigue_sample_count > 0 else final_fatigue
        )

        return {
            "total_tasks_created": self.total_tasks_created,
            "completed_tasks": self.completed_tasks,
            "routine_created": self.routine_created,
            "routine_completed": self.routine_completed,
            "completion_rate": completion_rate,

            "average_waiting_time": avg_wait,
            "max_waiting_time": self.max_waiting_time,
            "p95_waiting_time": p95_wait,

            "escalations": self.escalation_count,
            "light_to_medium": self.light_to_medium,
            "medium_to_heavy": self.medium_to_heavy,
            "heavy_secondary": self.heavy_secondary,

            "total_distance": self.total_distance,
            "average_fatigue": avg_fatigue,
            "final_fatigue": final_fatigue,
            "workload_std": workload_std,
            "normal_workload_std": normal_workload_std,
            "distance_std": distance_std,
            "final_fatigue_std": final_fatigue_std,
            "pending_tasks_left": len(self.pending_tasks),
            "pending_routine_tasks_left": len(self.pending_routine_tasks),

            "per_nurse_workload": {
                f"nurse_{n.nurse_id}": n.completed_tasks
                for n in self.nurses
            },

            "per_nurse_routine_workload": {
                f"nurse_{n.nurse_id}": n.completed_routine_tasks
                for n in self.nurses
            },

            "per_nurse_distance": {
                f"nurse_{n.nurse_id}": n.total_distance
                for n in self.nurses
            },

            "per_nurse_fatigue": {
                f"nurse_{n.nurse_id}": n.fatigue
                for n in self.nurses
            }
        }