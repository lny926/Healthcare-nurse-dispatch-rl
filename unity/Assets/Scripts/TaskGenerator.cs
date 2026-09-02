using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TimeWindowTaskSettings
{
    [Header("Window Name")]
    public string windowName;

    [Header("Time Range")]
    public int startHour;
    public int endHour;

    [Header("Random Spawn Interval (Simulated Seconds)")]
    public float minSpawnInterval = 480f; // 8 simulated minutes
    public float maxSpawnInterval = 720f; // 12 simulated minutes

    [Header("Task Type Weights")]
    public int lightWeight = 50;
    public int mediumWeight = 30;
    public int heavyWeight = 20;

    [Header("Task Count Weights Per Burst")]
    public int oneTaskWeight = 50;
    public int twoTaskWeight = 30;
    public int threeTaskWeight = 15;
    public int fourTaskWeight = 5;
}

public class TaskGenerator : MonoBehaviour
{
    [Header("All Rooms In Scene")]
    public List<RoomTask> allRooms = new List<RoomTask>();

    [Header("Enable Auto Generation")]
    public bool autoGenerate = true;

    [Header("Off-Peak / Normal Settings")]
    public TimeWindowTaskSettings normalSettings;

    [Header("Morning / Midday / Evening Settings")]
    public TimeWindowTaskSettings morningSettings;
    public TimeWindowTaskSettings middaySettings;
    public TimeWindowTaskSettings eveningSettings;

    private float timer = 0f;              // simulated seconds
    private float nextSpawnInterval = 300f; // simulated seconds

    void Start()
    {
        ScheduleNextSpawn();
    }

    void Update()
    {
        if (!autoGenerate) return;
        if (TimeManager.Instance == null) return;
        if (TaskManager.Instance == null) return;

        timer += TimeManager.Instance.GetDeltaSimSeconds();

        if (timer >= nextSpawnInterval)
        {
            timer = 0f;

            TimeWindowTaskSettings currentWindow = GetCurrentWindowSettings();

            int taskCount = GetWeightedTaskCount(currentWindow);

            for (int i = 0; i < taskCount; i++)
            {
                TryGenerateTask(currentWindow);
            }

            ScheduleNextSpawn();
        }
    }

    public void ResetGenerator()
    {
        timer = 0f;
        ScheduleNextSpawn();
    }

    private TimeWindowTaskSettings GetCurrentWindowSettings()
    {
        if (TimeManager.Instance == null)
        {
            return normalSettings;
        }

        if (morningSettings != null &&
            TimeManager.Instance.IsInTimeRange(morningSettings.startHour, morningSettings.endHour))
        {
            return morningSettings;
        }

        if (middaySettings != null &&
            TimeManager.Instance.IsInTimeRange(middaySettings.startHour, middaySettings.endHour))
        {
            return middaySettings;
        }

        if (eveningSettings != null &&
            TimeManager.Instance.IsInTimeRange(eveningSettings.startHour, eveningSettings.endHour))
        {
            return eveningSettings;
        }

        return normalSettings;
    }

    private void TryGenerateTask(TimeWindowTaskSettings settings)
    {
        if (settings == null) return;

        List<RoomTask> availableRooms = GetAvailableRooms();

        if (availableRooms.Count == 0)
        {
            Debug.Log("No available rooms for auto task generation.");
            return;
        }

        RoomTask selectedRoom = availableRooms[Random.Range(0, availableRooms.Count)];
        TaskType taskType = GetWeightedTaskType(settings);

        TaskManager.Instance.TryCreateTask(selectedRoom, taskType);

        Debug.Log("Auto-generated task at room: " + selectedRoom.roomID +
                  " | Type: " + taskType +
                  " | Window: " + settings.windowName);
    }

    private List<RoomTask> GetAvailableRooms()
    {
        List<RoomTask> available = new List<RoomTask>();

        foreach (RoomTask room in allRooms)
        {
            if (room != null && !room.hasTask)
            {
                available.Add(room);
            }
        }

        return available;
    }

    private TaskType GetWeightedTaskType(TimeWindowTaskSettings settings)
    {
        if (settings == null) return TaskType.Light;

        int totalWeight = settings.lightWeight + settings.mediumWeight + settings.heavyWeight;

        if (totalWeight <= 0)
        {
            return TaskType.Light;
        }

        int randomValue = Random.Range(0, totalWeight);

        if (randomValue < settings.lightWeight)
        {
            return TaskType.Light;
        }
        else if (randomValue < settings.lightWeight + settings.mediumWeight)
        {
            return TaskType.Medium;
        }
        else
        {
            return TaskType.Heavy;
        }
    }

    private void ScheduleNextSpawn()
    {
        TimeWindowTaskSettings currentWindow = GetCurrentWindowSettings();

        if (currentWindow == null)
        {
            nextSpawnInterval = 600f;
            return;
        }

        float min = Mathf.Min(currentWindow.minSpawnInterval, currentWindow.maxSpawnInterval);
        float max = Mathf.Max(currentWindow.minSpawnInterval, currentWindow.maxSpawnInterval);

        nextSpawnInterval = Random.Range(min, max);

        Debug.Log("Next task spawn in simulated seconds: " +
                  nextSpawnInterval.ToString("F1") +
                  " | Window: " + currentWindow.windowName);
    }

    private int GetWeightedTaskCount(TimeWindowTaskSettings settings)
    {
        if (settings == null) return 1;

        int totalWeight =
            settings.oneTaskWeight +
            settings.twoTaskWeight +
            settings.threeTaskWeight +
            settings.fourTaskWeight;

        if (totalWeight <= 0)
        {
            return 1;
        }

        int randomValue = Random.Range(0, totalWeight);

        if (randomValue < settings.oneTaskWeight)
        {
            return 1;
        }
        else if (randomValue < settings.oneTaskWeight + settings.twoTaskWeight)
        {
            return 2;
        }
        else if (randomValue < settings.oneTaskWeight + settings.twoTaskWeight + settings.threeTaskWeight)
        {
            return 3;
        }
        else
        {
            return 4;
        }
    }

    public string GetCurrentWindowName()
    {
        if (TimeManager.Instance == null)
        {
            return "Unknown";
        }

        TimeWindowTaskSettings currentWindow = GetCurrentWindowSettings();

        if (currentWindow != null && !string.IsNullOrEmpty(currentWindow.windowName))
        {
            return currentWindow.windowName;
        }

        return "Unknown";
    }
}