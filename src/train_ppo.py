import os
import csv
import random
from pathlib import Path

import torch
import torch.nn as nn
import torch.optim as optim
from torch.distributions import Categorical

from .rl_env import NurseDispatchEnv


class ActorCritic(nn.Module):
    def __init__(self, observation_size, action_size):
        super().__init__()

        self.shared = nn.Sequential(
            nn.Linear(observation_size, 128),
            nn.ReLU(),
            nn.Linear(128, 128),
            nn.ReLU()
        )

        self.actor = nn.Linear(128, action_size)
        self.critic = nn.Linear(128, 1)

    def forward(self, x):
        features = self.shared(x)
        logits = self.actor(features)
        value = self.critic(features)
        return logits, value


class PPOAgent:
    def __init__(
        self,
        observation_size,
        action_size,
        lr=3e-4,
        gamma=0.99,
        clip_epsilon=0.2,
        update_epochs=4
    ):
        self.gamma = gamma
        self.clip_epsilon = clip_epsilon
        self.update_epochs = update_epochs

        self.model = ActorCritic(observation_size, action_size)
        self.optimizer = optim.Adam(self.model.parameters(), lr=lr)

    def select_action(self, obs):
        obs_tensor = torch.tensor(obs, dtype=torch.float32).unsqueeze(0)

        logits, value = self.model(obs_tensor)
        dist = Categorical(logits=logits)

        action = dist.sample()
        log_prob = dist.log_prob(action)

        return (
            action.item(),
            log_prob.squeeze(),
            value.squeeze()
        )

    def update(self, observations, actions, old_log_probs, returns, advantages):
        observations = torch.tensor(observations, dtype=torch.float32)
        actions = torch.tensor(actions, dtype=torch.long)
        old_log_probs = torch.stack(old_log_probs).detach()
        returns = torch.tensor(returns, dtype=torch.float32)
        advantages = torch.tensor(advantages, dtype=torch.float32)

        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        for _ in range(self.update_epochs):
            logits, values = self.model(observations)
            values = values.squeeze()

            dist = Categorical(logits=logits)
            new_log_probs = dist.log_prob(actions)
            entropy = dist.entropy().mean()

            ratio = torch.exp(new_log_probs - old_log_probs)

            clipped_ratio = torch.clamp(
                ratio,
                1.0 - self.clip_epsilon,
                1.0 + self.clip_epsilon
            )

            actor_loss = -torch.min(
                ratio * advantages,
                clipped_ratio * advantages
            ).mean()

            critic_loss = nn.MSELoss()(values, returns)

            loss = actor_loss + 0.5 * critic_loss - 0.01 * entropy

            self.optimizer.zero_grad()
            loss.backward()
            self.optimizer.step()


def compute_returns_and_advantages(rewards, values, gamma):
    returns = []
    discounted_return = 0

    for reward in reversed(rewards):
        discounted_return = reward + gamma * discounted_return
        returns.insert(0, discounted_return)

    advantages = [
        returns[i] - values[i].item()
        for i in range(len(rewards))
    ]

    return returns, advantages


def train():
    output_dir = Path(__file__).parent / "outputs" / "ppo"
    output_dir.mkdir(parents=True, exist_ok=True)

    env = NurseDispatchEnv()

    agent = PPOAgent(
        observation_size=env.observation_size,
        action_size=env.action_space_size,
        lr=3e-4,
        gamma=0.99,
        clip_epsilon=0.2,
        update_epochs=4
    )

    num_episodes = 100

    log_file = output_dir / "ppo_training_log.csv"
    model_file = output_dir / "ppo_nurse_dispatch.pt"

    with open(log_file, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)

        writer.writerow([
            "episode",
            "seed",
            "total_reward",
            "completed_tasks",
            "completion_rate",
            "average_waiting_time_min",
            "p95_waiting_time_min",
            "escalations",
            "total_distance",
            "average_fatigue"
        ])

        for episode in range(1, num_episodes + 1):
            seed = episode
            obs = env.reset(seed=seed)

            observations = []
            actions = []
            log_probs = []
            values = []
            rewards = []

            done = False
            total_reward = 0
            final_info = {}

            while not done:
                action, log_prob, value = agent.select_action(obs)

                next_obs, reward, done, info = env.step(action)

                observations.append(obs)
                actions.append(action)
                log_probs.append(log_prob)
                values.append(value)
                rewards.append(reward)

                obs = next_obs
                total_reward += reward

                if done:
                    final_info = info

            returns, advantages = compute_returns_and_advantages(
                rewards,
                values,
                agent.gamma
            )

            agent.update(
                observations,
                actions,
                log_probs,
                returns,
                advantages
            )

            completed = final_info.get("completed_tasks", 0)
            completion_rate = final_info.get("completion_rate", 0)
            avg_wait_min = final_info.get("average_waiting_time", 0) / 60
            p95_wait_min = final_info.get("p95_waiting_time", 0) / 60
            escalations = final_info.get("escalations", 0)
            distance = final_info.get("total_distance", 0)
            fatigue = final_info.get("average_fatigue", 0)

            writer.writerow([
                episode,
                seed,
                total_reward,
                completed,
                completion_rate,
                avg_wait_min,
                p95_wait_min,
                escalations,
                distance,
                fatigue
            ])

            print(
                f"Episode {episode:03d} | "
                f"Reward={total_reward:.2f} | "
                f"Completed={completed} | "
                f"AvgWait={avg_wait_min:.2f} min | "
                f"P95={p95_wait_min:.2f} min | "
                f"Esc={escalations} | "
                f"Completion={completion_rate * 100:.1f}%"
            )

    torch.save(agent.model.state_dict(), model_file)

    print("\nTraining finished.")
    print(f"Training log saved to: {log_file}")
    print(f"Model saved to: {model_file}")


if __name__ == "__main__":
    train()