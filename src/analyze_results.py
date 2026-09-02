import csv
import math
from collections import defaultdict
from pathlib import Path


def load_csv(path):
    with open(path, "r", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def mean(values):
    values = [float(v) for v in values]
    return sum(values) / len(values) if values else 0


def std(values):
    values = [float(v) for v in values]
    if len(values) <= 1:
        return 0

    avg = mean(values)
    variance = sum((v - avg) ** 2 for v in values) / (len(values) - 1)
    return math.sqrt(variance)


def calculate_care_quality_score(summary):
    score = (
        summary["completion_rate_mean"] * 100
        - summary["average_waiting_time_min_mean"] * 2
        - summary["p95_waiting_time_min_mean"] * 1
        - summary["escalations_mean"] * 0.5
        - summary["average_fatigue_mean"] * 20
        - summary["workload_std_mean"] * 2
        - summary["distance_std_mean"] * 0.02
    )

    return score


def write_summary_csv(summary_rows, output_path):
    if not summary_rows:
        return

    fieldnames = list(summary_rows[0].keys())

    with open(output_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(summary_rows)


def main():
    base_dir = Path(__file__).parent
    csv_path = base_dir / "outputs" / "baseline_batch_results.csv"

    rows = load_csv(csv_path)

    grouped = defaultdict(list)

    for row in rows:
        grouped[row["strategy"]].append(row)

    summary_rows = []

    print("\n=== Strategy Summary ===")

    for strategy, items in grouped.items():
        summary = {
            "strategy": strategy,
            "runs": len(items),

            # Efficiency
            "completed_tasks_mean": mean([r["completed_tasks"] for r in items]),
            "completed_tasks_std": std([r["completed_tasks"] for r in items]),

            "completion_rate_mean": mean([r["completion_rate"] for r in items]),
            "completion_rate_std": std([r["completion_rate"] for r in items]),

            "average_waiting_time_min_mean": mean([r["average_waiting_time_min"] for r in items]),
            "average_waiting_time_min_std": std([r["average_waiting_time_min"] for r in items]),

            "p95_waiting_time_min_mean": mean([r["p95_waiting_time_min"] for r in items]),
            "p95_waiting_time_min_std": std([r["p95_waiting_time_min"] for r in items]),

            # Safety
            "escalations_mean": mean([r["escalations"] for r in items]),
            "escalations_std": std([r["escalations"] for r in items]),

            "light_to_medium_mean": mean([r["light_to_medium"] for r in items]),
            "light_to_medium_std": std([r["light_to_medium"] for r in items]),

            "medium_to_heavy_mean": mean([r["medium_to_heavy"] for r in items]),
            "medium_to_heavy_std": std([r["medium_to_heavy"] for r in items]),

            "heavy_secondary_mean": mean([r["heavy_secondary"] for r in items]),
            "heavy_secondary_std": std([r["heavy_secondary"] for r in items]),

            # Staff cost
            "total_distance_mean": mean([r["total_distance"] for r in items]),
            "total_distance_std": std([r["total_distance"] for r in items]),

            "average_fatigue_mean": mean([r["average_fatigue"] for r in items]),
            "average_fatigue_std": std([r["average_fatigue"] for r in items]),

            # Fairness
            "workload_std_mean": mean([r["workload_std"] for r in items]),
            "workload_std_std": std([r["workload_std"] for r in items]),

            "normal_workload_std_mean": mean([r["normal_workload_std"] for r in items]),
            "normal_workload_std_std": std([r["normal_workload_std"] for r in items]),

            "distance_std_mean": mean([r["distance_std"] for r in items]),
            "distance_std_std": std([r["distance_std"] for r in items]),

            "final_fatigue_std_mean": mean([r["final_fatigue_std"] for r in items]),
            "final_fatigue_std_std": std([r["final_fatigue_std"] for r in items]),

            # Remaining workload
            "pending_tasks_left_mean": mean([r["pending_tasks_left"] for r in items]),
            "pending_tasks_left_std": std([r["pending_tasks_left"] for r in items]),
        }

        summary["care_quality_score"] = calculate_care_quality_score(summary)

        summary_rows.append(summary)

        print(f"\nStrategy: {strategy}")
        print(f"Runs: {summary['runs']}")

        print("\nEfficiency:")
        print(f"Completed Tasks: {summary['completed_tasks_mean']:.2f} ± {summary['completed_tasks_std']:.2f}")
        print(f"Completion Rate: {summary['completion_rate_mean'] * 100:.2f}% ± {summary['completion_rate_std'] * 100:.2f}%")
        print(f"Avg Waiting Time: {summary['average_waiting_time_min_mean']:.2f} ± {summary['average_waiting_time_min_std']:.2f} min")
        print(f"P95 Waiting Time: {summary['p95_waiting_time_min_mean']:.2f} ± {summary['p95_waiting_time_min_std']:.2f} min")

        print("\nSafety:")
        print(f"Escalations: {summary['escalations_mean']:.2f} ± {summary['escalations_std']:.2f}")
        print(f"Light -> Medium: {summary['light_to_medium_mean']:.2f} ± {summary['light_to_medium_std']:.2f}")
        print(f"Medium -> Heavy: {summary['medium_to_heavy_mean']:.2f} ± {summary['medium_to_heavy_std']:.2f}")
        print(f"Heavy Secondary: {summary['heavy_secondary_mean']:.2f} ± {summary['heavy_secondary_std']:.2f}")

        print("\nStaff Cost:")
        print(f"Total Distance: {summary['total_distance_mean']:.2f} ± {summary['total_distance_std']:.2f}")
        print(f"Average Fatigue: {summary['average_fatigue_mean']:.3f} ± {summary['average_fatigue_std']:.3f}")

        print("\nFairness:")
        print(f"Workload Std: {summary['workload_std_mean']:.2f} ± {summary['workload_std_std']:.2f}")
        print(f"Normal Workload Std: {summary['normal_workload_std_mean']:.2f} ± {summary['normal_workload_std_std']:.2f}")
        print(f"Distance Std: {summary['distance_std_mean']:.2f} ± {summary['distance_std_std']:.2f}")
        print(f"Final Fatigue Std: {summary['final_fatigue_std_mean']:.3f} ± {summary['final_fatigue_std_std']:.3f}")

        print("\nComposite Score:")
        print(f"Care Quality Score: {summary['care_quality_score']:.2f}")

        print("\nRemaining:")
        print(f"Pending Tasks Left: {summary['pending_tasks_left_mean']:.2f} ± {summary['pending_tasks_left_std']:.2f}")

    output_path = base_dir / "outputs" / "strategy_summary.csv"
    write_summary_csv(summary_rows, output_path)

    print(f"\nSaved summary to: {output_path}")


if __name__ == "__main__":
    main()