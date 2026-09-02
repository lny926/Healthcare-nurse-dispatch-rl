using UnityEngine;
using TMPro;

public class StatsUI : MonoBehaviour
{
    public TMP_Text statsText;
    public TaskGenerator taskGenerator;

    void Update()
    {
        if (statsText == null || StatsManager.Instance == null || TaskManager.Instance == null) return;

        StatsManager stats = StatsManager.Instance;
        TaskManager taskManager = TaskManager.Instance;

        string currentWindow = "Unknown";
        if (taskGenerator != null)
        {
            currentWindow = taskGenerator.GetCurrentWindowName();
        }

        statsText.text =
            "=== Simulation Stats ===\n" +
            "Current Time: " + (TimeManager.Instance != null ? TimeManager.Instance.GetFormattedTime() : "00:00") + "\n" +
            "Current Window: " + currentWindow + "\n" +
            "Current Mode: " + taskManager.currentMode + "\n" +
            "Pending Mode: " + taskManager.pendingMode + "\n\n" +
            "Total Tasks: " + stats.totalTasksCreated + "\n" +
            "Completed Tasks: " + stats.completedTasks + "\n" +
            "Waiting Tasks: " + stats.waitingTasks + "\n" +
            "Average Waiting Time: " + stats.GetAverageWaitingTime().ToString("F1") + " s\n\n" +
            "Light Tasks: " + stats.lightTaskCount + "\n" +
            "Medium Tasks: " + stats.mediumTaskCount + "\n" +
            "Heavy Tasks: " + stats.heavyTaskCount + "\n\n" +
            "Escalations: " + StatsManager.Instance.escalationCount + "\n\n" +
            "Total Distance: " + stats.totalDistanceTraveled.ToString("F1") + "\n" +
            "Routine Created: " + stats.routineTaskCreated + "\n" +
            "Routine Completed: " + stats.routineTaskCompleted + "\n" +
            "Max Waiting Time: " + stats.maxWaitingTime.ToString("F1") + " s\n" +
            "P95 Waiting Time: " + stats.GetP95WaitingTime().ToString("F1") + " s\n" +
            "Escalations: " + stats.escalationCount + "\n" +
            "Light -> Medium: " + stats.lightToMediumEscalation + "\n" +
            "Medium -> Heavy: " + stats.mediumToHeavyEscalation + "\n" +
            "Heavy Secondary: " + stats.heavySecondaryCallCount + "\n" +
            "Completion Rate: " + (stats.GetCompletionRate() * 100f).ToString("F1") + "%\n";



    }
}