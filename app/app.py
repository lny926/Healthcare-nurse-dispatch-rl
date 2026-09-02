import copy
import sys
import threading
from functools import partial
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path

import pandas as pd
import streamlit as st
import streamlit.components.v1 as components

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

OUTPUTS_DIR = PROJECT_ROOT / "outputs"
UNITY_BUILD_DIR = PROJECT_ROOT / "unity_webgl_build"
UNITY_HOST = "127.0.0.1"
UNITY_START_PORT = 8000

from src.sim_core import CareSimulation
from src.config_loader import load_unity_settings
from src.evaluation_metrics import calculate_care_quality_score_from_results
from src.rl_env import NurseDispatchEnv
from src.ppo_policy import load_ppo_model, select_greedy_action

st.set_page_config(
    page_title="2D Caregiving Simulation",
    layout="wide"
)

st.title("2D Caregiving Simulation Experiment Platform")

st.write(
    "This platform allows users to run repeatable dispatch experiments "
    "and compare different nurse scheduling strategies."
)

outputs_dir = OUTPUTS_DIR

st.sidebar.header("Experiment Settings")

scenario = st.sidebar.selectbox(
    "Scenario Config",
    ["Medium Load", "Current Settings"]
)

strategy = st.sidebar.selectbox(
    "Dispatch Strategy",
    ["fcfs", "shortest_distance", "priority_first", "ai_score", "ppo_v3_500"]
)

simulation_hours = st.sidebar.number_input(
    "Simulation Hours",
    min_value=1,
    max_value=72,
    value=10
)

seed = st.sidebar.number_input(
    "Random Seed",
    min_value=1,
    max_value=999999,
    value=12345
)

batch_seed_start = st.sidebar.number_input(
    "Batch Seed Start",
    min_value=1,
    max_value=999999,
    value=1
)

batch_seed_end = st.sidebar.number_input(
    "Batch Seed End",
    min_value=1,
    max_value=999999,
    value=10
)

nurse_count = st.sidebar.number_input(
    "Nurse Count",
    min_value=1,
    max_value=20,
    value=6
)


def get_config_name_from_scenario(scenario):
    if scenario == "Current Settings":
        return "unity_current_settings.json"

    return "unity_medium_load_settings.json"



class UnityWebGLRequestHandler(SimpleHTTPRequestHandler):
    """Serve Unity WebGL files with useful MIME/encoding headers."""

    def guess_type(self, path):
        lower_path = path.lower()

        if lower_path.endswith((".wasm", ".wasm.gz", ".wasm.br")):
            return "application/wasm"
        if lower_path.endswith((".js", ".js.gz", ".js.br")):
            return "application/javascript"
        if lower_path.endswith((".data", ".data.gz", ".data.br")):
            return "application/octet-stream"
        if lower_path.endswith((".json", ".json.gz", ".json.br")):
            return "application/json"

        return super().guess_type(path)

    def end_headers(self):
        lower_path = self.path.lower()

        if lower_path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif lower_path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")

        self.send_header("Access-Control-Allow-Origin", "*")
        super().end_headers()

    def log_message(self, format, *args):
        # Keep the Streamlit terminal cleaner.
        pass


@st.cache_resource
def start_unity_server():
    """
    Start a small local HTTP server for the Unity WebGL build.

    The server starts automatically with Streamlit, so users who clone the
    repository do not need to run a second `python -m http.server` command.
    """
    index_file = UNITY_BUILD_DIR / "index.html"

    if not UNITY_BUILD_DIR.exists():
        raise FileNotFoundError(
            f"Unity WebGL build directory not found: {UNITY_BUILD_DIR}"
        )

    if not index_file.exists():
        raise FileNotFoundError(
            f"Unity WebGL index.html not found: {index_file}"
        )

    handler = partial(
        UnityWebGLRequestHandler,
        directory=str(UNITY_BUILD_DIR),
    )

    # Try 8000 first, then a few nearby ports if it is already occupied.
    last_error = None
    for port in range(UNITY_START_PORT, UNITY_START_PORT + 11):
        try:
            server = ThreadingHTTPServer((UNITY_HOST, port), handler)
            thread = threading.Thread(
                target=server.serve_forever,
                daemon=True,
            )
            thread.start()
            return server, port
        except OSError as exc:
            last_error = exc

    raise OSError(
        f"Could not start Unity WebGL server on ports "
        f"{UNITY_START_PORT}-{UNITY_START_PORT + 10}"
    ) from last_error


st.header("Unity WebGL Demonstration")
st.info(
    """
    This Unity-based prototype visualises the elderly care ward,
    including nurse movement, task generation, dispatch decisions,
    fatigue accumulation, and task escalation behaviour.
    """
)

try:
    _, unity_port = start_unity_server()

    components.iframe(
        f"http://{UNITY_HOST}:{unity_port}",
        height=550,
        scrolling=False
    )
except Exception as exc:
    st.warning(
        "Unity WebGL demonstration is unavailable. "
        "Make sure the complete `unity_webgl_build` folder is located "
        "in the project root."
    )
    st.caption(str(exc))

st.subheader("Selected Settings")

st.write({
    "scenario": scenario,
    "config_file": get_config_name_from_scenario(scenario),
    "strategy": strategy,
    "simulation_hours": simulation_hours,
    "seed": seed,
    "batch_seed_start": batch_seed_start,
    "batch_seed_end": batch_seed_end,
    "nurse_count": nurse_count
})

def run_ppo_experiment(simulation_hours, seed, nurse_count, scenario):
    config_name = get_config_name_from_scenario(scenario)
    config = copy.deepcopy(load_unity_settings(config_name))

    config["experiment"]["duration_hours"] = int(simulation_hours)
    config["experiment"]["random_seed"] = int(seed)
    config["scene"]["nurse_count"] = int(nurse_count)

    env = NurseDispatchEnv(config=config)

    model_path = PROJECT_ROOT / "outputs" / "ppo_v3_500_model.pt"

    model = load_ppo_model(
        model_path=model_path,
        observation_size=env.observation_size,
        action_size=env.action_space_size
    )

    observation = env.reset(seed=int(seed))
    done = False
    info = {}

    while not done:
        action = select_greedy_action(
            model=model,
            observation=observation
        )

        observation, reward, done, info = env.step(action)

    return info

def run_single_experiment(strategy, simulation_hours, seed, nurse_count, scenario):
    if strategy == "ppo_v3_500":
        return run_ppo_experiment(
            simulation_hours=simulation_hours,
            seed=seed,
            nurse_count=nurse_count,
            scenario=scenario
        )

    config_name = get_config_name_from_scenario(scenario)
    config = copy.deepcopy(load_unity_settings(config_name))

    config["experiment"]["dispatch_mode"] = strategy
    config["experiment"]["duration_hours"] = int(simulation_hours)
    config["experiment"]["random_seed"] = int(seed)
    config["scene"]["nurse_count"] = int(nurse_count)

    sim = CareSimulation(config)
    results = sim.run()

    return results


def build_result_row(strategy, seed, results, scenario):
    care_score = calculate_care_quality_score_from_results(results)

    return {
        "scenario": scenario,
        "strategy": strategy,
        "seed": int(seed),

        "total_tasks_created": results["total_tasks_created"],
        "completed_tasks": results["completed_tasks"],
        "routine_created": results["routine_created"],
        "routine_completed": results["routine_completed"],
        "completion_rate": results["completion_rate"],

        "average_waiting_time_min": results["average_waiting_time"] / 60,
        "max_waiting_time_min": results["max_waiting_time"] / 60,
        "p95_waiting_time_min": results["p95_waiting_time"] / 60,

        "escalations": results["escalations"],
        "light_to_medium": results["light_to_medium"],
        "medium_to_heavy": results["medium_to_heavy"],
        "heavy_secondary": results["heavy_secondary"],

        "total_distance": results["total_distance"],
        "average_fatigue": results["average_fatigue"],
        "final_fatigue": results["final_fatigue"],

        "workload_std": results["workload_std"],
        "normal_workload_std": results["normal_workload_std"],
        "distance_std": results["distance_std"],
        "final_fatigue_std": results["final_fatigue_std"],

        "pending_tasks_left": results["pending_tasks_left"],
        "pending_routine_tasks_left": results["pending_routine_tasks_left"],

        "care_quality_score": care_score,
    }


def run_batch_experiment(strategy, simulation_hours, seed_start, seed_end, nurse_count, scenario):
    rows = []

    for current_seed in range(int(seed_start), int(seed_end) + 1):
        results = run_single_experiment(
            strategy=strategy,
            simulation_hours=simulation_hours,
            seed=current_seed,
            nurse_count=nurse_count,
            scenario=scenario
        )

        rows.append(
            build_result_row(
                strategy=strategy,
                seed=current_seed,
                results=results,
                scenario=scenario
            )
        )

    return pd.DataFrame(rows)


def run_full_comparison(simulation_hours, seed_start, seed_end, nurse_count, scenario):
    strategies = [
        "fcfs",
        "shortest_distance",
        "priority_first",
        "ai_score",
        "ppo_v3_500"
    ]

    all_rows = []

    progress = st.progress(0)
    status_text = st.empty()

    total_runs = len(strategies) * (int(seed_end) - int(seed_start) + 1)
    finished_runs = 0

    for current_strategy in strategies:
        for current_seed in range(int(seed_start), int(seed_end) + 1):
            status_text.write(
                f"Running {current_strategy}, seed {current_seed}..."
            )

            results = run_single_experiment(
                strategy=current_strategy,
                simulation_hours=simulation_hours,
                seed=current_seed,
                nurse_count=nurse_count,
                scenario=scenario
            )

            all_rows.append(
                build_result_row(
                    strategy=current_strategy,
                    seed=current_seed,
                    results=results,
                    scenario=scenario
                )
            )

            finished_runs += 1
            progress.progress(finished_runs / total_runs)

    status_text.write("Full comparison completed.")

    return pd.DataFrame(all_rows)


def summarize_results(result_df):
    summary_df = result_df.groupby(["scenario", "strategy"]).agg({
        "completed_tasks": ["mean", "std"],
        "completion_rate": ["mean", "std"],
        "average_waiting_time_min": ["mean", "std"],
        "p95_waiting_time_min": ["mean", "std"],
        "escalations": ["mean", "std"],
        "total_distance": ["mean", "std"],
        "average_fatigue": ["mean", "std"],
        "workload_std": ["mean", "std"],
        "distance_std": ["mean", "std"],
        "final_fatigue_std": ["mean", "std"],
        "care_quality_score": ["mean", "std"],
        "pending_tasks_left": ["mean", "std"],
    })

    return summary_df


def flatten_summary(summary_df):
    flat = summary_df.copy()
    flat.columns = [
        "_".join(col).strip()
        for col in flat.columns.values
    ]
    flat = flat.reset_index()
    return flat


def show_summary_charts(summary_flat):
    st.subheader("Comparison Charts")

    chart_metrics = [
        ("care_quality_score_mean", "Care Quality Score"),
        ("completion_rate_mean", "Completion Rate"),
        ("average_waiting_time_min_mean", "Average Waiting Time (min)"),
        ("p95_waiting_time_min_mean", "P95 Waiting Time (min)"),
        ("escalations_mean", "Escalations"),
        ("average_fatigue_mean", "Average Fatigue"),
        ("workload_std_mean", "Workload Std"),
        ("distance_std_mean", "Distance Std"),
    ]

    for metric, title in chart_metrics:
        if metric in summary_flat.columns:
            st.write(title)
            st.bar_chart(
                summary_flat.set_index("strategy")[metric]
            )


st.header("Run Single Experiment")

if st.button("Run Single Experiment"):
    results = run_single_experiment(
        strategy=strategy,
        simulation_hours=simulation_hours,
        seed=seed,
        nurse_count=nurse_count,
        scenario=scenario
    )

    care_score = calculate_care_quality_score_from_results(results)

    st.success("Simulation Complete")

    col1, col2, col3, col4 = st.columns(4)

    col1.metric(
        "Completion Rate",
        f"{results['completion_rate'] * 100:.1f}%"
    )

    col2.metric(
        "Avg Waiting Time",
        f"{results['average_waiting_time'] / 60:.2f} min"
    )

    col3.metric(
        "Escalations",
        int(results["escalations"])
    )

    col4.metric(
        "Care Quality Score",
        f"{care_score:.2f}"
    )

    st.subheader("Full Results")

    main_results = {
        k: v for k, v in results.items()
        if not isinstance(v, dict)
    }

    main_results["care_quality_score"] = care_score

    st.dataframe(
        pd.DataFrame([main_results]),
        use_container_width=True
    )

    st.subheader("Per Nurse Results")

    per_nurse_df = pd.DataFrame({
        "normal_workload": results["per_nurse_workload"],
        "routine_workload": results["per_nurse_routine_workload"],
        "distance": results["per_nurse_distance"],
        "fatigue": results["per_nurse_fatigue"],
    })

    st.dataframe(
        per_nurse_df,
        use_container_width=True
    )

    single_csv = pd.DataFrame([
        build_result_row(strategy, seed, results, scenario)
    ]).to_csv(index=False).encode("utf-8")

    st.download_button(
        label="Download Single Result CSV",
        data=single_csv,
        file_name=f"{scenario}_{strategy}_seed_{int(seed)}_result.csv",
        mime="text/csv"
    )


st.header("Run Batch Experiment")

if st.button("Run Batch Experiment"):
    if int(batch_seed_end) < int(batch_seed_start):
        st.error("Batch Seed End must be greater than or equal to Batch Seed Start.")
    else:
        batch_df = run_batch_experiment(
            strategy=strategy,
            simulation_hours=simulation_hours,
            seed_start=batch_seed_start,
            seed_end=batch_seed_end,
            nurse_count=nurse_count,
            scenario=scenario
        )

        st.success("Batch Experiment Complete")

        st.subheader("Batch Results")
        st.dataframe(
            batch_df,
            use_container_width=True
        )

        st.subheader("Batch Summary")
        summary_df = summarize_results(batch_df)
        summary_flat = flatten_summary(summary_df)

        st.dataframe(
            summary_flat,
            use_container_width=True
        )

        show_summary_charts(summary_flat)

        batch_csv = batch_df.to_csv(index=False).encode("utf-8")

        st.download_button(
            label="Download Batch Results CSV",
            data=batch_csv,
            file_name=f"{scenario}_{strategy}_batch_results.csv",
            mime="text/csv"
        )

        summary_csv = summary_flat.to_csv(index=False).encode("utf-8")

        st.download_button(
            label="Download Batch Summary CSV",
            data=summary_csv,
            file_name=f"{scenario}_{strategy}_batch_summary.csv",
            mime="text/csv"
        )


st.header("Run Full Strategy Comparison")

if st.button("Run Full Comparison"):
    if int(batch_seed_end) < int(batch_seed_start):
        st.error("Batch Seed End must be greater than or equal to Batch Seed Start.")
    else:
        comparison_df = run_full_comparison(
            simulation_hours=simulation_hours,
            seed_start=batch_seed_start,
            seed_end=batch_seed_end,
            nurse_count=nurse_count,
            scenario=scenario
        )

        st.success("Full Strategy Comparison Complete")

        st.subheader("Full Comparison Results")
        st.dataframe(
            comparison_df,
            use_container_width=True
        )

        st.subheader("Full Comparison Summary")
        comparison_summary = summarize_results(comparison_df)
        comparison_summary_flat = flatten_summary(comparison_summary)

        st.dataframe(
            comparison_summary_flat,
            use_container_width=True
        )

        show_summary_charts(comparison_summary_flat)

        comparison_csv = comparison_df.to_csv(index=False).encode("utf-8")

        st.download_button(
            label="Download Full Comparison Results CSV",
            data=comparison_csv,
            file_name=f"{scenario}_full_strategy_comparison_results.csv",
            mime="text/csv"
        )

        comparison_summary_csv = comparison_summary_flat.to_csv(index=False).encode("utf-8")

        st.download_button(
            label="Download Full Comparison Summary CSV",
            data=comparison_summary_csv,
            file_name=f"{scenario}_full_strategy_comparison_summary.csv",
            mime="text/csv"
        )


st.header("Existing Baseline Summary")

summary_path = outputs_dir / "strategy_summary.csv"

if summary_path.exists():
    df = pd.read_csv(summary_path)

    st.dataframe(
        df,
        use_container_width=True
    )

    if "care_quality_score" in df.columns:
        st.subheader("Existing Care Quality Score")
        st.bar_chart(
            df.set_index("strategy")["care_quality_score"]
        )
else:
    st.warning("No strategy_summary.csv found. Please run analyze_results.py first.")


st.header("Metric Explanation")

with st.expander("View metric definitions"):
    st.markdown("""
    **Completion Rate**  
    The percentage of generated tasks that were completed during the simulation.

    **Average Waiting Time**  
    The average time a normal task waited before being handled by a nurse.

    **P95 Waiting Time**  
    The 95th percentile waiting time. It shows the high-end waiting delay experienced by difficult cases.

    **Escalations**  
    The number of task severity increases, including Light to Medium, Medium to Heavy, and Heavy Secondary calls.

    **Average Fatigue**  
    The average fatigue level of nurses during the simulation.

    **Workload Std**  
    The standard deviation of workload across nurses. A lower value means the workload is more balanced.

    **Distance Std**  
    The standard deviation of travelled distance across nurses. A lower value means movement burden is more balanced.

    **Care Quality Score**  
    A composite score combining completion rate, waiting time, escalation count, fatigue, workload balance, and distance balance.
    """)


with st.expander("Scenario description"):
    st.markdown("""
    **Medium Load** uses `unity_medium_load_settings.json` and represents the main experimental scenario.

    **Current Settings** uses `unity_current_settings.json` and represents the original Unity-aligned configuration.

    The system allows users to compare different dispatch strategies under the same configuration and random seed range.
    """)
