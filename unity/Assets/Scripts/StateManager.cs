using System.Collections.Generic;
using UnityEngine;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;

    [Header("Task Statistics")]
    public int totalTasksCreated = 0;
    public int completedTasks = 0;
    public int waitingTasks = 0;

    public int lightTaskCount = 0;
    public int mediumTaskCount = 0;
    public int heavyTaskCount = 0;

    [Header("Routine Task Statistics")]
    public int routineTaskCreated = 0;
    public int routineTaskCompleted = 0;

    [Header("Waiting Time Statistics")]
    public float totalWaitingTime = 0f;
    public float maxWaitingTime = 0f;

    private List<float> completedWaitingTimes = new List<float>();

    [Header("Nurse Statistics")]
    public float totalDistanceTraveled = 0f;
    public float averageFatigue = 0f;

    [Header("Escalation Stats")]
    public int escalationCount = 0;
    public int lightToMediumEscalation = 0;
    public int mediumToHeavyEscalation = 0;
    public int heavySecondaryCallCount = 0;

    private void Awake()
    {
        Instance = this;
    }

    // 普通任务创建时调用
    public void RegisterTaskCreated(TaskType taskType)
    {
        totalTasksCreated++;
        waitingTasks++;

        switch (taskType)
        {
            case TaskType.Light:
                lightTaskCount++;
                break;
            case TaskType.Medium:
                mediumTaskCount++;
                break;
            case TaskType.Heavy:
                heavyTaskCount++;
                break;
        }
    }

    // Routine / Medication 任务创建时调用
    public void RegisterRoutineTaskCreated()
    {
        routineTaskCreated++;
        waitingTasks++;
    }

    // 普通任务或 Routine 任务开始被护士处理时调用
    public void RegisterTaskStarted()
    {
        waitingTasks = Mathf.Max(0, waitingTasks - 1);
    }

    // 普通任务完成时调用
    public void RegisterTaskCompleted(float waitingTime)
    {
        completedTasks++;

        totalWaitingTime += waitingTime;
        maxWaitingTime = Mathf.Max(maxWaitingTime, waitingTime);
        completedWaitingTimes.Add(waitingTime);
    }

    // Routine / Medication 任务完成时调用
    public void RegisterRoutineTaskCompleted()
    {
        routineTaskCompleted++;
    }

    public float GetAverageWaitingTime()
    {
        if (completedTasks == 0) return 0f;
        return totalWaitingTime / completedTasks;
    }

    public float GetP95WaitingTime()
    {
        if (completedWaitingTimes.Count == 0) return 0f;

        List<float> sorted = new List<float>(completedWaitingTimes);
        sorted.Sort();

        int index = Mathf.CeilToInt(sorted.Count * 0.95f) - 1;
        index = Mathf.Clamp(index, 0, sorted.Count - 1);

        return sorted[index];
    }

    public void AddDistance(float distance)
    {
        totalDistanceTraveled += distance;
    }

    // 总升级次数
    public void RegisterEscalation()
    {
        escalationCount++;
    }

    // Light -> Medium
    public void RegisterLightToMediumEscalation()
    {
        escalationCount++;
        lightToMediumEscalation++;
    }

    // Medium -> Heavy
    public void RegisterMediumToHeavyEscalation()
    {
        escalationCount++;
        mediumToHeavyEscalation++;
    }

    // Heavy 等太久后的 secondary call
    public void RegisterHeavySecondaryCall()
    {
        escalationCount++;
        heavySecondaryCallCount++;
    }

    public void UpdateAverageFatigue(List<NurseAction> nurses)
    {
        if (nurses == null || nurses.Count == 0)
        {
            averageFatigue = 0f;
            return;
        }

        float totalFatigue = 0f;

        foreach (NurseAction nurse in nurses)
        {
            if (nurse != null)
            {
                totalFatigue += nurse.fatigue;
            }
        }

        averageFatigue = totalFatigue / nurses.Count;
    }

    public float GetCompletionRate()
    {
        int totalGenerated = totalTasksCreated + routineTaskCreated;
        int totalCompleted = completedTasks + routineTaskCompleted;

        if (totalGenerated == 0) return 0f;

        return (float)totalCompleted / totalGenerated;
    }

    public void ResetStats()
    {
        totalTasksCreated = 0;
        completedTasks = 0;
        waitingTasks = 0;

        lightTaskCount = 0;
        mediumTaskCount = 0;
        heavyTaskCount = 0;

        routineTaskCreated = 0;
        routineTaskCompleted = 0;

        totalWaitingTime = 0f;
        maxWaitingTime = 0f;
        completedWaitingTimes.Clear();

        totalDistanceTraveled = 0f;

        escalationCount = 0;
        lightToMediumEscalation = 0;
        mediumToHeavyEscalation = 0;
        heavySecondaryCallCount = 0;
    }
}