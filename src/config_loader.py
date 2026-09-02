import json
from pathlib import Path


def load_unity_settings(config_name="unity_medium_load_settings.json"):
    base_dir = Path(__file__).resolve().parent.parent
    config_path = base_dir / "config" / config_name

    if not config_path.exists():
        raise FileNotFoundError(f"Config file not found: {config_path}")

    with open(config_path, "r", encoding="utf-8") as f:
        return json.load(f)


def print_config_summary(config):
    print("\n=== Unity Config Summary ===")

    print("\nExperiment:")
    print(f"Start Time: {config['experiment']['start_hour']:02d}:{config['experiment']['start_minute']:02d}")
    print(f"Duration Hours: {config['experiment']['duration_hours']}")
    print(f"Seed: {config['experiment']['random_seed']}")
    print(f"Dispatch Mode: {config['experiment']['dispatch_mode']}")

    print("\nScene:")
    print(f"Nurse Count: {config['scene']['nurse_count']}")
    print(f"Room Count: {config['scene']['room_count']}")
    print(f"Room IDs: {config['scene']['room_ids']}")

    print("\nTask Windows:")
    for name in ["normal", "morning", "midday", "evening"]:
        window = config["task_generation"][name]
        print(
            f"{name}: {window['start_hour']} - {window['end_hour']} | "
            f"spawn {window['min_spawn_interval_seconds']} - {window['max_spawn_interval_seconds']} sec | "
            f"weights {window['task_type_weights']}"
        )

    print("\nNurse:")
    print(f"Move Speed: {config['nurse']['move_speed']}")
    print(f"Shift Length: {config['nurse']['shift_length_hours']} hours")
    print(f"Heavy Fatigue Increase: {config['nurse']['fatigue']['heavy_task_fatigue_increase']}")

    print("\nRoutine:")
    print(f"Enabled: {config['routine_tasks']['enabled']}")
    print(f"Rooms Per Trigger: {config['routine_tasks']['rooms_per_trigger']}")
    print(f"Interval Seconds: {config['routine_tasks']['routine_interval_seconds']}")


if __name__ == "__main__":
    config = load_unity_settings()
    print_config_summary(config)