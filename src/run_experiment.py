from .config_loader import load_unity_settings
from .sim_core import CareSimulation


def print_results(config, results):
    exp = config["experiment"]

    print("\n=== Unity-Aligned Experiment Results ===")
    print(f"Strategy: {exp['dispatch_mode']}")
    print(f"Seed: {exp['random_seed']}")
    print(f"Start Time: {exp['start_hour']:02d}:{exp['start_minute']:02d}")
    print(f"Duration Hours: {exp['duration_hours']}")
    print("--------------------------------------")

    print(f"Total Tasks Created: {results['total_tasks_created']}")
    print(f"Completed Tasks: {results['completed_tasks']}")
    print(f"Routine Created: {results['routine_created']}")
    print(f"Routine Completed: {results['routine_completed']}")
    print(f"Completion Rate: {results['completion_rate'] * 100:.1f}%")

    print(f"\nAverage Waiting Time: {results['average_waiting_time'] / 60:.2f} minutes")
    print(f"Max Waiting Time: {results['max_waiting_time'] / 60:.2f} minutes")
    print(f"P95 Waiting Time: {results['p95_waiting_time'] / 60:.2f} minutes")

    print(f"\nEscalations: {results['escalations']}")
    print(f"Light -> Medium: {results['light_to_medium']}")
    print(f"Medium -> Heavy: {results['medium_to_heavy']}")
    print(f"Heavy Secondary: {results['heavy_secondary']}")

    print(f"\nTotal Distance: {results['total_distance']:.2f}")
    print(f"Average Fatigue: {results['average_fatigue']:.3f}")

    print(f"Workload Std: {results['workload_std']:.2f}")
    print(f"Normal Workload Std: {results['normal_workload_std']:.2f}")
    print(f"Distance Std: {results['distance_std']:.2f}")
    print(f"Final Fatigue Std: {results['final_fatigue_std']:.3f}")
    
    print(f"Pending Tasks Left: {results['pending_tasks_left']}")
    print(f"Pending Routine Tasks Left: {results['pending_routine_tasks_left']}")

    print("\nPer Nurse Normal Workload:")
    for nurse, value in results["per_nurse_workload"].items():
        print(f"{nurse}: {value}")

    print("\nPer Nurse Routine Workload:")
    for nurse, value in results["per_nurse_routine_workload"].items():
        print(f"{nurse}: {value}")

    print("\nPer Nurse Distance:")
    for nurse, value in results["per_nurse_distance"].items():
        print(f"{nurse}: {value:.2f}")

    print("\nPer Nurse Fatigue:")
    for nurse, value in results["per_nurse_fatigue"].items():
        print(f"{nurse}: {value:.3f}")


def main():
    config = load_unity_settings()

    sim = CareSimulation(config)
    results = sim.run()

    print_results(config, results)


if __name__ == "__main__":
    main()