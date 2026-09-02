# Healthcare Nurse Dispatching with Reinforcement Learning

A simulation-based research platform for evaluating nurse dispatching strategies in an elderly care environment.

This project combines **Python simulation, PyTorch reinforcement learning, Streamlit experimentation, and Unity visualisation** to investigate how different nurse dispatching strategies affect task throughput, waiting time, escalation risk, nurse fatigue, travel distance, and workload balance.

The project was developed as part of my **Master of Artificial Intelligence research project at the University of Auckland**.

---

## Quick Start

### Requirements

- Windows 10 / 11
- Python 3.10+
- Git

> This project was developed and tested on Windows.  
> macOS and Linux have not been formally tested.

### 1. Clone the repository

```bash
git clone https://github.com/lny926/Healthcare-nurse-dispatch-rl.git
cd Healthcare-nurse-dispatch-rl
```

### 2. Install dependencies

```bash
python -m pip install -r requirements.txt
```

### 3. Run the experiment platform

```bash
python -m streamlit run app/app.py
```

Streamlit will provide a local address, typically:

```text
http://localhost:8501
```

Open this address in a browser to access the experiment platform.

The Unity WebGL demonstration is started automatically by the Streamlit application, so a separate Unity installation or Unity WebGL server is not required.

---

## What Can Be Explored?

The Streamlit application provides three main experiment modes:

### Single Experiment

Run one dispatching strategy using a selected:

- Simulation scenario
- Simulation duration
- Random seed
- Nurse count

The application displays:

- Completion rate
- Average waiting time
- P95 waiting time
- Escalation count
- Care Quality Score
- Full experiment results
- Per-nurse workload
- Per-nurse travel distance
- Per-nurse fatigue

Results can also be downloaded as CSV files.

### Batch Experiment

Run the same strategy across multiple random seeds.

This allows the performance of a dispatching strategy to be evaluated under different stochastic task-generation conditions rather than relying on a single simulation run.

### Full Strategy Comparison

Run all supported dispatching strategies across the same seed range.

The platform automatically:

- Executes all experiments
- Aggregates mean and standard deviation
- Produces comparison tables
- Generates comparison charts
- Exports the results as CSV files

---

## Unity WebGL Demonstration

The Streamlit interface includes a Unity WebGL demonstration of the original elderly care simulation prototype.

The Unity environment visualises:

- Resident rooms
- Nurse movement
- Dynamic care-task generation
- Task severity
- Routine care tasks
- Task escalation
- Nurse fatigue
- Dispatching behaviour
- Real-time operational statistics

The Unity prototype uses a two-sided ward layout with a central nurse station and twelve resident rooms.

Task severity is represented visually:

- **Green** — Light task
- **Yellow** — Medium task
- **Red** — Heavy task
- **Blue** — Routine task

The Unity environment was primarily developed as a visual prototype for observing simulation behaviour, while the Python simulation core was later developed for faster experimentation, repeated evaluation, and reinforcement learning integration.

---

# Key Results

The final experimental comparison evaluated five nurse dispatching strategies under the same **Medium Load** simulation setting:

- FCFS
- Shortest-distance
- Priority-first
- AI Score
- PPO v3-500

The results showed that **no single dispatching strategy dominated every performance metric**.

| Strategy | Completed Tasks | Completion Rate | Avg Wait (min) | P95 Wait (min) | Escalations | Avg Fatigue | Travel Distance | Care Quality Score |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **AI Score** | 161.0 | **99.2%** | 3.45 | 9.84 | 28.8 | 0.123 | 9,942.39 | **56.78** |
| FCFS | 127.0 | 94.5% | 6.29 | 18.45 | 107.2 | 0.310 | 11,324.91 | -6.22 |
| **PPO v3-500** | 137.3 | 90.2% | 2.98 | **9.01** | **22.7** | **0.083** | **7,961.68** | 53.54 |
| Priority-first | 121.0 | 92.1% | 7.02 | 22.69 | 126.8 | 0.347 | 10,287.83 | -22.71 |
| **Shortest-distance** | **162.2** | **99.2%** | **2.67** | 9.15 | 29.0 | 0.146 | 8,384.47 | 56.73 |

## Main Findings

### AI Score

AI Score achieved the highest overall **Care Quality Score of 56.78**.

Its manually designed multi-factor decision logic provided a strong balance between task completion, waiting time, task severity, nurse distance, and nurse condition.

### Shortest-Distance

Shortest-distance achieved:

- The highest average number of completed tasks: **162.2**
- A completion rate of **99.2%**
- The lowest average waiting time: **2.67 minutes**

This demonstrates that a simple distance-based heuristic can perform extremely well when travel time is an important operational bottleneck and sufficient nurse capacity is available.

### PPO v3-500

PPO v3-500 achieved a Care Quality Score of **53.54**, ranking third overall.

However, PPO achieved the best result on several care-risk and nurse-burden indicators:

- Lowest P95 waiting time: **9.01 minutes**
- Lowest escalation count: **22.7**
- Lowest average fatigue: **0.083**
- Lowest total travel distance: **7,961.68**

PPO therefore learned a more conservative dispatching pattern that reduced severe waiting cases, escalation risk, nurse fatigue, and travel burden.

This improvement came at the cost of lower overall task throughput.

---

## Key Research Takeaway

The experiments did **not** show that reinforcement learning automatically outperforms well-designed heuristic scheduling strategies.

Instead, different strategies produced different operational trade-offs.

**AI Score and shortest-distance** performed best for throughput and overall Care Quality Score.

**PPO v3-500** completed fewer tasks but performed better on several risk and staff-burden indicators.

The main finding is therefore:

> Nurse dispatching is a multi-objective optimisation problem.  
> Maximising task completion alone does not necessarily minimise waiting risk, staff fatigue, escalation, or movement burden.

This also demonstrates the importance of benchmarking AI approaches against strong and interpretable baseline methods rather than assuming that a more complex model will automatically produce better operational performance.

---

# Problem Definition

Nurse dispatching in elderly care is a dynamic resource-allocation problem.

Care tasks may occur:

- At different times
- In different resident rooms
- With different severity levels
- Alongside routine care requirements

At the same time, nurses may have different:

- Locations
- Availability
- Current workloads
- Fatigue levels
- Shift states

A dispatching decision can therefore affect more than immediate task completion.

For example:

- Selecting the nearest nurse may reduce travel time but increase workload imbalance.
- Prioritising severe tasks may cause lower-priority tasks to wait and later escalate.
- Maximising task throughput may increase travel burden or fatigue.
- Reducing nurse workload may reduce overall task completion.

The project therefore models nurse dispatching as a **dynamic multi-objective decision-making problem**.

---

# System Architecture

The framework combines visual simulation, configurable experimentation, dispatching algorithms, reinforcement learning, and performance analysis.

```text
                 Unity Visual Simulation
                         |
                         v
                Configuration Files
                         |
                         v
                Python Simulation Core
                         |
                         v
              Dispatching Strategies
                         |
        +----------------+----------------+
        |        |          |       |     |
       FCFS   Distance   Priority  AI   PPO
        |        |          |       |     |
        +----------------+----------------+
                         |
                         v
                Evaluation Metrics
                         |
                         v
              Streamlit Experiment UI
                         |
                         v
          Tables / Charts / CSV Export
```

---

## Unity Visual Simulation

The Unity prototype represents the elderly care environment visually.

It includes:

- Ward layout
- Resident rooms
- Nurse station
- Nurse agents
- Nurse movement
- Dynamic task generation
- Routine care tasks
- Task escalation
- Nurse fatigue
- Shift logic
- Dispatch strategy controls
- Real-time simulation statistics

The visual environment was useful during development for validating task generation, nurse movement, escalation logic, fatigue behaviour, and dispatch decisions.

---

## Configuration Layer

Simulation settings are stored separately using JSON configuration files.

The main configurations include:

```text
config/unity_medium_load_settings.json
config/unity_current_settings.json
```

Configuration files control parameters such as:

- Simulation duration
- Start time
- Random seed
- Nurse count
- Room layout
- Task-generation frequency
- Task-type probabilities
- Task duration
- Routine-task generation
- Escalation thresholds
- Nurse movement speed
- Fatigue behaviour
- Shift duration
- Dispatch strategy parameters

Separating configuration from simulation logic makes the experiments easier to reproduce and modify.

---

## Python Simulation Core

The Python simulation core reproduces the main operational logic of the Unity prototype while supporting faster repeated experimentation.

The simulation models:

- Normal care-task generation
- Routine care tasks
- Task severity
- Task escalation
- Nurse availability
- Nurse movement
- Travel distance
- Task duration
- Nurse fatigue
- Fatigue recovery
- Rest behaviour
- Shift changes
- Workload
- Task completion
- Waiting-time statistics

The Python implementation allows experiments to be repeated across multiple seeds and dispatching strategies efficiently.

---

# Simulation Environment

The final strategy comparison primarily uses the **Medium Load** scenario.

| Experimental Setting | Value |
|---|---|
| Scenario | Medium Load |
| Simulation start time | 05:00 |
| Configured simulation duration | 10 simulated hours |
| Nurses | 6 |
| Resident rooms | 12 |
| Room IDs | A1–A6 and B1–B6 |
| Normal task types | Light, Medium, Heavy |
| Routine tasks | Enabled |
| Routine interval | 7,200 simulated seconds |
| Routine rooms per trigger | 2 |
| Escalation threshold | Approximately 8–12 minutes |
| Strategies | FCFS, Shortest-distance, Priority-first, AI Score, PPO |
| Final evaluation seeds | 1–10 |

Normal tasks are generated dynamically.

Task-generation frequency and severity distribution vary across different time periods, allowing the simulation to represent changing care demand rather than using a constant workload throughout the day.

Routine tasks are generated periodically to represent recurring care activities such as medication support or regular resident care.

---

# Dispatching Strategies

## 1. First-Come, First-Served (FCFS)

FCFS selects the earliest pending task.

### Strengths

- Simple
- Interpretable
- Easy to implement
- Preserves task-arrival order

### Limitations

FCFS does not explicitly consider:

- Nurse distance
- Nurse fatigue
- Task severity
- Future workload

---

## 2. Shortest-Distance

Shortest-distance evaluates available nurse-task pairs and selects the pair with the shortest travel distance.

### Strengths

- Reduces travel time
- Reduces general response delay
- Highly effective under manageable workload

### Limitations

Repeatedly selecting the nearest nurse may produce uneven workload distribution.

---

## 3. Priority-First

Priority-first selects the highest-severity pending task before assigning an available nurse.

### Strengths

- Gives direct attention to severe tasks
- Simple urgency-based decision logic

### Limitations

Lower-priority tasks may remain unhandled for longer periods and eventually escalate.

In the final experiment, this strategy produced the highest escalation count and nurse fatigue.

---

## 4. AI Score

AI Score is a manually designed multi-factor heuristic.

The score considers:

- Nurse-task distance
- Nurse fatigue
- Task priority
- Task waiting time

The system evaluates available nurse-task combinations and selects the pair with the best combined score.

AI Score provides an intermediate approach between simple single-rule heuristics and reinforcement learning.

---

## 5. PPO Reinforcement Learning

The PPO agent operates as a **high-level dispatching strategy selector**.

Rather than directly controlling nurse movement, the PPO policy observes the current simulation state and chooses one of five dispatching behaviours.

This design allows PPO to dynamically switch between different dispatching principles depending on the current state of the system.

---

# PPO Reinforcement Learning Design

## Observation Space

The PPO agent receives a fixed **20-dimensional observation vector**.

### Nurse State

For six nurses:

- Fatigue level × 6
- Availability state × 6

### System State

The remaining observations describe the current workload:

- Pending normal-task count
- Pending routine-task count
- Average waiting time
- Maximum waiting time
- Ratio of light tasks
- Ratio of medium tasks
- Ratio of heavy tasks
- Simulation progress

Several values are normalised before being passed to the neural network.

---

## Action Space

The PPO agent uses **five discrete actions**.

| Action | Dispatching Behaviour |
|---:|---|
| 0 | Shortest-distance nurse-task pair |
| 1 | Highest-priority task + nearest available nurse |
| 2 | Lowest-fatigue nurse + longest-waiting task |
| 3 | FCFS task + nearest available nurse |
| 4 | AI Score-based nurse-task selection |

After an action is selected, the simulation advances through a short decision window while task generation, waiting time, nurse states, fatigue, shift logic, and task completion continue to update.

---

# PPO Reward Design

Rather than rewarding only a single immediate event, PPO evaluates how the overall system state changes after each decision.

A system-performance function is calculated as:

```text
Φ(s) =
12 × CompletedTasks
+ 80 × CompletionRate
- 2 × AverageWaitingTime
- P95WaitingTime
- 4 × Escalations
- 6 × HeavySecondaryCalls
- TotalDistance / 1000
- 12 × AverageFatigue
- 4 × PendingNormalTasks
- 2 × PendingRoutineTasks
```

The PPO reward is then:

```text
Reward(t) = Φ(state[t+1]) - Φ(state[t])
```

This reward encourages:

- Task completion
- High completion rate

while discouraging:

- Long waiting times
- High-end waiting delays
- Task escalation
- Heavy secondary calls
- Excessive travel
- High nurse fatigue
- Unfinished tasks

The reward was designed to reflect the multi-objective nature of nurse dispatching rather than optimising only task throughput.

---

# PPO Network Architecture

The PPO policy uses an actor-critic neural network.

```text
20-dimensional observation
          |
          v
   Linear Layer (128)
          |
        ReLU
          |
          v
   Linear Layer (128)
          |
        ReLU
          |
       +--+--+
       |     |
       v     v
     Actor  Critic
       |     |
  5 actions Value
```

Main model settings:

| Parameter | Value |
|---|---|
| Hidden layers | 2 |
| Hidden units | 128 per layer |
| Activation | ReLU |
| Optimiser | Adam |
| Learning rate | 3e-4 |
| Discount factor (γ) | 0.99 |
| PPO clipping range | 0.2 |
| Value-loss coefficient | 0.5 |
| Entropy coefficient | 0.01 |
| PPO update epochs | 4 |

During training, actions are sampled from a categorical distribution produced by the actor network.

During final evaluation, the trained policy uses greedy action selection.

---

# PPO Training Experiments

Different PPO training durations and random seeds were evaluated.

| PPO Model | Episodes | Completion Rate | Avg Wait (min) | P95 Wait (min) | Escalations | Avg Fatigue | Care Quality Score |
|---|---:|---:|---:|---:|---:|---:|---:|
| PPO v3-300, Seed 1 | 300 | 0.910 | 1.96 | 7.35 | 17.2 | 0.086 | 58.55 |
| PPO v3-300, Seed 2 | 300 | 0.868 | 4.86 | 15.41 | 76.8 | 0.210 | 12.11 |
| **PPO v3-500** | **500** | **0.902** | **2.98** | **9.01** | **22.7** | **0.083** | **53.54** |
| PPO v3-1000 | 1000 | 0.859 | 4.97 | 17.36 | 91.6 | 0.236 | 1.63 |

The 300-episode experiments showed strong sensitivity to training seed.

The 1000-episode model also demonstrated that longer training did not automatically produce a stronger dispatching policy.

PPO v3-500 was selected as the final model because it provided a more stable and balanced result across:

- Task completion
- Waiting time
- Escalation control
- Nurse fatigue
- Training behaviour

This model was therefore used in the final strategy comparison.

---

# Evaluation Metrics

Nurse dispatching performance is evaluated from multiple perspectives.

## Task Throughput

### Completed Tasks

Number of care tasks completed during the experiment.

### Completion Rate

Percentage of generated normal and routine tasks completed during the simulation.

---

## Waiting-Time Performance

### Average Waiting Time

Mean waiting time of completed normal care tasks.

### P95 Waiting Time

95th percentile waiting time.

This metric captures severe waiting cases that may not be visible from the average waiting time alone.

---

## Care-Risk Metrics

### Task Escalation

Tasks may escalate when they wait too long:

```text
Light → Medium
Medium → Heavy
Heavy → Secondary Call
```

A lower escalation count indicates stronger control of delayed care.

---

## Nurse Burden

### Average Fatigue

Average nurse fatigue measured throughout the simulation.

### Final Fatigue

Fatigue levels remaining at the end of the experiment.

---

## Travel Burden

### Total Distance

Total distance travelled by all nurses.

### Distance Standard Deviation

Measures whether travel burden is distributed evenly between nurses.

---

## Workload Balance

### Workload Standard Deviation

Measures how evenly task completion is distributed across nurses.

A lower value indicates a more balanced workload.

---

# Care Quality Score

A composite **Care Quality Score (CQS)** is used to summarise performance across multiple operational objectives.

```text
CQS =
100 × CompletionRate
- 2 × AverageWaitingTime
- P95WaitingTime
- 0.5 × Escalations
- 20 × AverageFatigue
- 2 × WorkloadStd
- 0.02 × DistanceStd
```

A higher score represents better overall performance under the selected weighting scheme.

The Care Quality Score is intended as an internal experimental comparison metric.

It does **not** replace individual metrics and should not be interpreted as a universal clinical care-quality standard.

---

# Technology Stack

## Machine Learning

- Python
- PyTorch
- Proximal Policy Optimization (PPO)
- Actor-Critic neural networks
- Reinforcement Learning

## Simulation and Analysis

- Python
- NumPy
- Pandas
- Matplotlib
- Stochastic simulation
- JSON-based configuration
- Repeated experimental evaluation

## Application

- Streamlit

## Visual Simulation

- Unity
- Unity WebGL

## Development

- Git
- GitHub
- Jupyter Notebook

---

# Repository Structure

```text
Healthcare-nurse-dispatch-rl/
│
├── app/
│   └── app.py
│
├── config/
│   ├── unity_current_settings.json
│   └── unity_medium_load_settings.json
│
├── notebooks/
│
├── outputs/
│   ├── ppo_v3_500_model.pt
│   └── ...
│
├── src/
│   ├── __init__.py
│   ├── sim_core.py
│   ├── rl_env.py
│   ├── ppo_policy.py
│   ├── train_ppo.py
│   ├── evaluate_ppo.py
│   ├── evaluation_metrics.py
│   ├── run_experiment.py
│   ├── batch_runner.py
│   ├── analyze_results.py
│   ├── plot_results.py
│   └── config_loader.py
│
├── unity/
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
│
├── unity_webgl_build/
│   ├── index.html
│   ├── Build/
│   └── TemplateData/
│
├── requirements.txt
├── .gitignore
└── README.md
```

---

# Main Source Modules

## `sim_core.py`

Core discrete-time healthcare simulation.

Responsible for:

- Task generation
- Routine tasks
- Task escalation
- Nurse availability
- Nurse movement
- Fatigue
- Shift logic
- Dispatch execution
- Performance metrics

## `rl_env.py`

Reinforcement-learning environment.

Responsible for:

- Building PPO observations
- Defining the five-action decision space
- Applying RL-selected dispatching behaviour
- Advancing the simulation
- Calculating the PPO reward

## `ppo_policy.py`

Defines the actor-critic network used to load and evaluate trained PPO models.

## `train_ppo.py`

Contains the PPO training implementation.

## `evaluate_ppo.py`

Evaluates trained PPO policies and compares their results with baseline dispatching strategies.

## `evaluation_metrics.py`

Calculates the Care Quality Score and related evaluation measures.

## `config_loader.py`

Loads simulation parameters from JSON configuration files.

## `batch_runner.py`

Runs repeated experiments across multiple random seeds.

## `analyze_results.py`

Aggregates experiment results and calculates summary statistics.

## `plot_results.py`

Generates experiment comparison figures using Matplotlib.

## `app/app.py`

Provides the Streamlit experiment interface and automatically serves the Unity WebGL demonstration.

---

# Limitations

This project is a **research prototype rather than a deployable healthcare scheduling system**.

Important limitations include:

- Simulation parameters were manually designed rather than calibrated using real elderly care facility data.
- Task generation and task duration contain stochastic assumptions.
- Final conclusions primarily reflect the Medium Load scenario.
- Real elderly care environments contain additional complexity not represented by the simulation.
- Nurse capability and resident-specific requirements are simplified.
- Staff absence is not modelled in detail.
- Communication and documentation workload are not represented.
- Path congestion is not modelled.
- Complex multi-nurse tasks are not included.
- PPO performance is sensitive to random seed.
- PPO performance is sensitive to reward design.
- PPO performance is sensitive to training duration and action design.
- Reward weights were manually selected.
- Care Quality Score weights were manually selected.
- The system has not been validated with real nurses, residents, or facility managers.
- The model has not been evaluated for clinical safety or real-world deployment.

Results should therefore be interpreted as comparisons within a controlled stochastic simulation environment rather than predictions of real elderly care operations.

---

# Future Work

Potential extensions include:

### Real-World Calibration

Use real or expert-informed healthcare data for:

- Task-arrival distributions
- Care-task duration
- Escalation thresholds
- Nurse walking time
- Routine-care schedules
- Shift patterns
- Fatigue parameters

### Sensitivity Analysis

Evaluate different:

- Nurse counts
- Task-generation frequencies
- Staff-shortage scenarios
- Morning and evening peaks
- Emergency-heavy workloads
- Escalation thresholds
- Routine-care frequencies

### More Rigorous RL Evaluation

Train each PPO configuration across multiple random seeds and report confidence intervals.

Potential experiments include reward-function ablation studies to determine the effect of:

- Fatigue penalty
- Escalation penalty
- Distance penalty
- Waiting-time penalty
- Pending-task penalty

### Additional Reinforcement Learning Algorithms

Potential comparisons include:

- A2C
- DQN
- TRPO
- Multi-agent reinforcement learning

### Mathematical Optimisation

Future versions could introduce optimisation-based scheduling approaches such as:

- Mixed-Integer Linear Programming
- Constraint Programming
- Metaheuristics
- Multi-objective optimisation

These approaches could also be combined with reinforcement learning to investigate hybrid scheduling systems.

### More Realistic Healthcare Modelling

Possible additions include:

- Nurse skill levels
- Nurse qualifications
- Resident-specific care requirements
- Temporary staff absence
- Emergency events
- Path congestion
- Interruptions
- Multi-nurse care tasks

### Human-in-the-Loop Decision Support

The Streamlit platform could be extended so that users can:

- Adjust dispatching weights
- Inspect model recommendations
- Compare alternative decisions
- Override dispatch suggestions
- Explore scenario assumptions interactively

---

# Academic Context

This repository is based on my 2026 Master of Artificial Intelligence dissertation:

**Simulation-Based Evaluation of Nurse Dispatching Strategies in Elderly Care**

School of Computer Science  
The University of Auckland

The research investigates:

- Healthcare scheduling
- Dynamic nurse dispatching
- Resource allocation
- Simulation-based experimentation
- Reinforcement learning
- PPO
- Heuristic scheduling
- Multi-objective evaluation
- Nurse workload
- Care-risk indicators

The project should be understood as a research and portfolio demonstration rather than a production healthcare system.

---

# Author

**Naiyao (Jared) Li**

Master of Artificial Intelligence  
University of Auckland  
Auckland, New Zealand