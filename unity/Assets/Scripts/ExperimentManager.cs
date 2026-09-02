using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance;

    [Header("Experiment Control")]
    public bool autoStartOnPlay = false;
    public bool isExperimentRunning = false;

    [Header("Start Time Settings")]
    [Range(0, 23)]
    public int startHour = 8;

    [Range(0, 59)]
    public int startMinute = 0;

    [Header("Duration Settings")]
    public float experimentDurationHours = 10f;

    [Header("Time Speed Settings")]
    public float experimentTimeScale = 300f; // 300 = 1秒现实时间等于5分钟模拟时间

    [Header("Random Seed Settings")]
    public bool useRandomSeed = true;
    public int randomSeed = 12345;

    [Header("Dispatch Mode Settings")]
    public DispatchMode experimentMode = DispatchMode.FCFS;

    [Header("References")]
    public TaskManager taskManager;

    private float experimentDurationSeconds = 0f;

    public static bool IsExperimentMode = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartExperiment();
        }
    }

    private void Update()
    {
        if (!isExperimentRunning) return;
        if (TimeManager.Instance == null) return;

        float elapsed = TimeManager.Instance.GetTotalSimulatedSeconds();

        if (elapsed >= experimentDurationSeconds)
        {
            StopExperiment();
        }
    }


    //  按钮调用：Start

    public void StartExperimentFromButton()
    {
        IsExperimentMode = true;

        if (isExperimentRunning)
        {
            Debug.Log("Experiment already running.");
            return;
        }

        StartExperiment();
    }


    //  按钮调用：Stop

    public void StopExperimentFromButton()
    {
        StopExperiment();

        IsExperimentMode = false;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetTimeScale(60f);
        }
    }

    //  开始实验

    public void StartExperiment()
    {
        IsExperimentMode = true;

        //  恢复 Unity 时间（防止之前暂停）
        Time.timeScale = 1f;

        Debug.Log("Starting experiment...");

        if (taskManager == null)
        {
            taskManager = TaskManager.Instance;
        }

        // 1. 设置随机种子
        if (useRandomSeed)
        {
            Random.InitState(randomSeed);
            Debug.Log("Experiment seed set to: " + randomSeed);
        }

        // 2. 重置系统
        if (taskManager != null)
        {
            taskManager.ResetSimulation();
        }

        // 3. 设置开始时间 & 加速
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetStartTime(startHour, startMinute);
            TimeManager.Instance.SetTimeScale(experimentTimeScale);
        }

        // 4. 设置调度模式
        if (taskManager != null)
        {
            taskManager.currentMode = experimentMode;
            taskManager.pendingMode = experimentMode;
        }

        // 5. 设置实验时长（秒）
        experimentDurationSeconds = experimentDurationHours * 60f * 60f;

        isExperimentRunning = true;

        Debug.Log(">>> EXPERIMENT MODE ACTIVE <<<");

        Debug.Log("Experiment started | Mode: " + experimentMode +
                  " | Start Time: " + startHour.ToString("00") + ":" + startMinute.ToString("00") +
                  " | Duration Hours: " + experimentDurationHours +
                  " | TimeScale: " + experimentTimeScale);
    }


    //  核心：停止实验

    public void StopExperiment()
    {
        if (!isExperimentRunning)
        {
            Debug.Log("Experiment is not running.");
            return;
        }

        isExperimentRunning = false;

        IsExperimentMode = false;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetTimeScale(60f);
        }

        Debug.Log("Experiment stopped.");

        PrintExperimentSummary();

        CSVExporter.ExportExperimentResult(
            experimentMode.ToString(),
            randomSeed,
            experimentTimeScale,
            experimentDurationHours,

            StatsManager.Instance.totalTasksCreated,
            StatsManager.Instance.completedTasks,

            StatsManager.Instance.GetCompletionRate(),

            StatsManager.Instance.GetAverageWaitingTime(),
            StatsManager.Instance.GetP95WaitingTime(),

            StatsManager.Instance.escalationCount,

            StatsManager.Instance.totalDistanceTraveled,

            StatsManager.Instance.routineTaskCompleted,
            StatsManager.Instance.averageFatigue
            );
    }


    // 输出实验结果

    private void PrintExperimentSummary()
    {
        if (StatsManager.Instance == null)
        {
            Debug.LogWarning("No StatsManager found.");
            return;
        }

        StatsManager stats = StatsManager.Instance;

        Debug.Log(
            "===== Experiment Summary =====\n" +
            "Mode: " + experimentMode + "\n" +
            "Seed: " + randomSeed + "\n" +
            "Start Time: " + startHour.ToString("00") + ":" + startMinute.ToString("00") + "\n" +
            "Duration Hours: " + experimentDurationHours + "\n\n" +

            "Total Tasks: " + stats.totalTasksCreated + "\n" +
            "Completed Tasks: " + stats.completedTasks + "\n" +
            "Routine Created: " + stats.routineTaskCreated + "\n" +
            "Routine Completed: " + stats.routineTaskCompleted + "\n" +
            "Completion Rate: " + (stats.GetCompletionRate() * 100f).ToString("F1") + "%\n\n" +

            "Average Waiting Time: " + stats.GetAverageWaitingTime().ToString("F2") + "\n" +
            "Max Waiting Time: " + stats.maxWaitingTime.ToString("F2") + "\n" +
            "P95 Waiting Time: " + stats.GetP95WaitingTime().ToString("F2") + "\n\n" +

            "Escalations: " + stats.escalationCount + "\n" +
            "Light -> Medium: " + stats.lightToMediumEscalation + "\n" +
            "Medium -> Heavy: " + stats.mediumToHeavyEscalation + "\n" +
            "Heavy Secondary: " + stats.heavySecondaryCallCount + "\n\n" +

            "Total Distance: " + stats.totalDistanceTraveled.ToString("F2")
        );
    }
}