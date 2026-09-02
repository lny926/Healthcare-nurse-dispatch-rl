using System.Collections.Generic;
using UnityEngine;

public class RoutineTaskManager : MonoBehaviour
{
    public static RoutineTaskManager Instance;

    [Header("All Routine Rooms")]
    public List<RoomRoutineTask> allRooms = new List<RoomRoutineTask>();

    [Header("Routine Settings")]
    public int roomsPerTrigger = 2;

    // Every 2 simulated hours
    public float routineIntervalSeconds = 7200f;

    private float nextTriggerTotalTime = 0f;

    private void Start()
    {
        ResetRoutineTaskManager();
    }

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (TimeManager.Instance == null) return;

        float totalTime = TimeManager.Instance.GetTotalSimulatedSeconds();

        // Trigger all missed routine intervals if timeScale is high
        while (totalTime >= nextTriggerTotalTime)
        {
            TriggerRoutineTasks();
            nextTriggerTotalTime += routineIntervalSeconds;
        }
    }

    private void TriggerRoutineTasks()
    {
        List<RoomRoutineTask> availableRooms = GetAvailableRooms();

        if (availableRooms.Count == 0)
        {
            Debug.Log("No available rooms for routine task.");
            return;
        }

        int count = Mathf.Min(roomsPerTrigger, availableRooms.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, availableRooms.Count);

            RoomRoutineTask room = availableRooms[randomIndex];

            room.ActivateMedicationTask();

            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.AddRoutineTask(room);
            }

            availableRooms.RemoveAt(randomIndex);
        }

        Debug.Log("Routine medication tasks triggered at total simulated time: " +
                  TimeManager.Instance.GetTotalSimulatedSeconds().ToString("F0"));
    }

    private List<RoomRoutineTask> GetAvailableRooms()
    {
        List<RoomRoutineTask> result = new List<RoomRoutineTask>();

        foreach (RoomRoutineTask room in allRooms)
        {
            if (room != null && !room.hasMedicationTask)
            {
                result.Add(room);
            }
        }

        return result;
    }

    public void ResetRoutineTaskManager()
    {
        nextTriggerTotalTime = routineIntervalSeconds;
    }

    public void ResetAllRoutineRooms()
    {
        foreach (RoomRoutineTask room in allRooms)
        {
            if (room != null)
            {
                room.ResetRoutineTask();
            }
        }

        nextTriggerTotalTime = routineIntervalSeconds;

        Debug.Log("All routine rooms reset.");
    }
}