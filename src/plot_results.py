import csv
from pathlib import Path

import matplotlib.pyplot as plt


def load_csv(path):
    with open(path, "r", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def get_column(rows, column):
    return [float(row[column]) for row in rows]


def plot_bar(rows, metric_mean, metric_std, title, ylabel, output_file):
    strategies = [row["strategy"] for row in rows]
    means = get_column(rows, metric_mean)
    stds = get_column(rows, metric_std)

    plt.figure(figsize=(9, 5))
    plt.bar(strategies, means, yerr=stds, capsize=5)
    plt.title(title)
    plt.xlabel("Dispatch Strategy")
    plt.ylabel(ylabel)
    plt.xticks(rotation=25)
    plt.tight_layout()
    plt.savefig(output_file, dpi=300)
    plt.close()

def plot_single_bar(rows, metric, title, ylabel, output_file):
    strategies = [row["strategy"] for row in rows]
    values = get_column(rows, metric)

    plt.figure(figsize=(9, 5))
    plt.bar(strategies, values)
    plt.title(title)
    plt.xlabel("Dispatch Strategy")
    plt.ylabel(ylabel)
    plt.xticks(rotation=25)
    plt.tight_layout()
    plt.savefig(output_file, dpi=300)
    plt.close()

def main():
    base_dir = Path(__file__).parent
    input_file = base_dir / "outputs" / "strategy_summary.csv"

    plot_dir = base_dir / "outputs" / "plots"
    plot_dir.mkdir(parents=True, exist_ok=True)

    rows = load_csv(input_file)

    plot_bar(
        rows,
        "average_waiting_time_min_mean",
        "average_waiting_time_min_std",
        "Average Waiting Time by Strategy",
        "Average Waiting Time (minutes)",
        plot_dir / "average_waiting_time.png"
    )

    plot_bar(
        rows,
        "p95_waiting_time_min_mean",
        "p95_waiting_time_min_std",
        "P95 Waiting Time by Strategy",
        "P95 Waiting Time (minutes)",
        plot_dir / "p95_waiting_time.png"
    )

    plot_bar(
        rows,
        "escalations_mean",
        "escalations_std",
        "Escalations by Strategy",
        "Number of Escalations",
        plot_dir / "escalations.png"
    )

    plot_bar(
        rows,
        "completion_rate_mean",
        "completion_rate_std",
        "Completion Rate by Strategy",
        "Completion Rate",
        plot_dir / "completion_rate.png"
    )

    plot_bar(
        rows,
        "total_distance_mean",
        "total_distance_std",
        "Total Distance by Strategy",
        "Total Distance",
        plot_dir / "total_distance.png"
    )

    plot_single_bar(
        rows,
        "care_quality_score",
        "Care Quality Score by Strategy",
        "Care Quality Score",
        plot_dir / "care_quality_score.png"
    )

    plot_bar(
        rows,
        "average_fatigue_mean",
        "average_fatigue_std",
        "Average Fatigue by Strategy",
        "Average Fatigue",
        plot_dir / "average_fatigue.png"
    )

    print("Plots generated successfully.")
    print(f"Saved to: {plot_dir}")


if __name__ == "__main__":
    main()