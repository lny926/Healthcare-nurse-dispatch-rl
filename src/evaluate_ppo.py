import csv
import copy
from pathlib import Path

import torch

from .config_loader import load_unity_settings
from .sim_core import CareSimulation
from .rl_env import NurseDispatchEnv
from .train_ppo import ActorCritic


def run_baseline(strategy, seed, base_config):
    config = copy.deepcopy(base_config)

    config["experiment"]["dispatch_mode"] = strategy
    config["experiment"]["random_seed"] = seed

    sim = CareSimulation(config)
    results = sim.run()

    return results


def run_ppo(seed, base_config, model_path):
    env = NurseDispatchEnv(config=base_config)

    model = ActorCritic(
        observation_size=env.observation_size,
        action_size=env.action_space_size
    )

    model.load_state_dict(torch.load(model_path, map_location="cpu"))
    model.eval()

    obs = env.reset(seed=seed)

    done = False
    final_info = {}

    while not done:
        obs_tensor = torch.tensor(obs, dtype=torch.float32).unsqueeze(0)

        with torch.no_grad():
            logits, value = model(obs_tensor)
            action = torch.argmax(logits, dim=-1).item()

        obs, reward, done, info = env.step(action)

        if done:
            final_info = info

    return final_info


def flatten_result(strategy, seed, results):
    return {
        "strategy": strategy,
        "seed": seed,

        "total_tasks_created": results["total_tasks_created"],
        "completed_tasks": results["completed_tasks"],
        "routine_created": results["routine_created"],
        "routine_completed": results["routine_completed"],
        "completion_rate": results["completion_rate"],

        "average_waiting_time_min": results["average_waiting_time"] / 60,
        "p95_waiting_time_min": results["p95_waiting_time"] / 60,
        "max_waiting_time_min": results["max_waiting_time"] / 60,

        "escalations": results["escalations"],
        "light_to_medium": results["light_to_medium"],
        "medium_to_heavy": results["medium_to_heavy"],
        "heavy_secondary": results["heavy_secondary"],

        "total_distance": results["total_distance"],
        "average_fatigue": results["average_fatigue"],

        "pending_tasks_left": results["pending_tasks_left"],
        "pending_routine_tasks_left": results["pending_routine_tasks_left"],
    }


def main():
    base_dir = Path(__file__).parent
    output_dir = base_dir / "outputs" / "ppo"
    output_dir.mkdir(parents=True, exist_ok=True)

    base_config = load_unity_settings()

    model_path = output_dir / "ppo_policy_selector.pt"

    if not model_path.exists():
        raise FileNotFoundError(f"PPO model not found: {model_path}")

    strategies = [
        "fcfs",
        "shortest_distance",
        "priority_first",
        "ai_score"
    ]

    seeds = list(range(1, 11))

    rows = []

    for seed in seeds:
        for strategy in strategies:
            results = run_baseline(strategy, seed, base_config)
            row = flatten_result(strategy, seed, results)
            rows.append(row)

            print(
                f"Baseline | {strategy} | seed={seed} | "
                f"Completed={results['completed_tasks']} | "
                f"AvgWait={results['average_waiting_time'] / 60:.2f} | "
                f"Esc={results['escalations']}"
            )

        ppo_results = run_ppo(seed, base_config, model_path)
        row = flatten_result("ppo", seed, ppo_results)
        rows.append(row)

        print(
            f"PPO      | seed={seed} | "
            f"Completed={ppo_results['completed_tasks']} | "
            f"AvgWait={ppo_results['average_waiting_time'] / 60:.2f} | "
            f"Esc={ppo_results['escalations']}"
        )

    output_file = output_dir / "ppo_vs_baseline_results.csv"

    fieldnames = list(rows[0].keys())

    with open(output_file, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print("\nEvaluation finished.")
    print(f"Saved to: {output_file}")


if __name__ == "__main__":
    main()