import csv
import copy
from pathlib import Path

from .config_loader import load_unity_settings
from .sim_core import CareSimulation


def flatten_results(strategy, seed, config, results):
    row = {
        "strategy": strategy,
        "seed": seed,

        "duration_hours": config["experiment"]["duration_hours"],
        "start_hour": config["experiment"]["start_hour"],
        "start_minute": config["experiment"]["start_minute"],

        "total_tasks_created": results["total_tasks_created"],
        "completed_tasks": results["completed_tasks"],
        "routine_created": results["routine_created"],
        "routine_completed": results["routine_completed"],
        "completion_rate": results["completion_rate"],

        "average_waiting_time_sec": results["average_waiting_time"],
        "average_waiting_time_min": results["average_waiting_time"] / 60,

        "max_waiting_time_sec": results["max_waiting_time"],
        "max_waiting_time_min": results["max_waiting_time"] / 60,

        "p95_waiting_time_sec": results["p95_waiting_time"],
        "p95_waiting_time_min": results["p95_waiting_time"] / 60,

        "escalations": results["escalations"],
        "light_to_medium": results["light_to_medium"],
        "medium_to_heavy": results["medium_to_heavy"],
        "heavy_secondary": results["heavy_secondary"],

        "total_distance": results["total_distance"],
        "average_fatigue": results["average_fatigue"],

        "workload_std": results["workload_std"],
        "normal_workload_std": results["normal_workload_std"],
        "distance_std": results["distance_std"],
        "final_fatigue_std": results["final_fatigue_std"],

        "pending_tasks_left": results["pending_tasks_left"],
        "pending_routine_tasks_left": results["pending_routine_tasks_left"],
    }

    for nurse, value in results["per_nurse_workload"].items():
        row[f"{nurse}_normal_workload"] = value

    for nurse, value in results["per_nurse_routine_workload"].items():
        row[f"{nurse}_routine_workload"] = value

    for nurse, value in results["per_nurse_distance"].items():
        row[f"{nurse}_distance"] = value

    for nurse, value in results["per_nurse_fatigue"].items():
        row[f"{nurse}_final_fatigue"] = value

    return row


def run_batch():
    base_config = load_unity_settings()

    strategies = [
        "fcfs",
        "shortest_distance",
        "priority_first",
        "ai_score"
    ]

    seeds = list(range(1, 11))

    rows = []

    for strategy in strategies:
        for seed in seeds:
            config = copy.deepcopy(base_config)

            config["experiment"]["dispatch_mode"] = strategy
            config["experiment"]["random_seed"] = seed

            sim = CareSimulation(config)
            results = sim.run()

            row = flatten_results(strategy, seed, config, results)
            rows.append(row)

            print(
                f"Finished | Strategy={strategy} | Seed={seed} | "
                f"Completed={results['completed_tasks']} | "
                f"AvgWait={results['average_waiting_time'] / 60:.2f} min | "
                f"P95={results['p95_waiting_time'] / 60:.2f} min | "
                f"Escalations={results['escalations']} | "
                f"Completion={results['completion_rate'] * 100:.1f}%"
            )

    output_dir = Path(__file__).parent / "outputs"
    output_dir.mkdir(exist_ok=True)

    output_file = output_dir / "baseline_batch_results.csv"

    fieldnames = sorted(rows[0].keys())

    with open(output_file, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print("\nBatch experiment finished.")
    print(f"Saved to: {output_file}")


if __name__ == "__main__":
    run_batch()