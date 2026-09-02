using System.Collections.Generic;
using UnityEngine;

public class RoomTask : MonoBehaviour
{
    [Header("房间信息")]
    public string roomID;

    [Header("房间颜色显示")]
    public SpriteRenderer roomRenderer;
    public Color normalColor = Color.white;
    public Color lightTaskColor = Color.green;
    public Color mediumTaskColor = Color.yellow;
    public Color heavyTaskColor = Color.red;

    [Header("路径设置")]
    public List<Transform> goPath = new List<Transform>();
    public bool autoGenerateReturnPath = true;
    public bool removeRoomPointFromReturn = true;
    public List<Transform> manualReturnPath = new List<Transform>();

    [Header("任务状态")]
    public bool hasTask = false;
    public TaskType currentTaskType;
    public float taskDuration = 0f;
    public float waitingTime = 0f;
    public bool isBeingHandled = false;

    [Header("Escalation Settings")]
    public float minEscalationMinutes = 8f;
    public float maxEscalationMinutes = 12f;

    public float escalationThreshold = 30f;

    [Header("Safety Timeout")]
    public float maxHandleTimeoutMinutes = 30f;
    private float handleTimer = 0f;

    private bool heavySecondaryCallTriggered = false;

    private void Start()
    {
        SetNormalColor();
    }

    private void Update()
    {
        CheckHandleTimeout();

        if (hasTask && !isBeingHandled)
        {
            if (ExperimentManager.IsExperimentMode)
            {
                waitingTime += TimeManager.Instance.GetDeltaSimSeconds();
            }
            else
            {
                waitingTime += Time.deltaTime;
            }

            UpdateTaskColorByProgress();
            CheckEscalation();
        }
    }

    private void CheckHandleTimeout()
    {
        if (!hasTask)
        {
            handleTimer = 0f;
            return;
        }

        if (!isBeingHandled)
        {
            handleTimer = 0f;
            return;
        }

        float simMinutes;

        if (TimeManager.Instance != null)
        {
            simMinutes = TimeManager.Instance.GetDeltaSimSeconds() / 60f;
        }
        else
        {
            simMinutes = Time.deltaTime / 60f;
        }

        handleTimer += simMinutes;

        if (handleTimer >= maxHandleTimeoutMinutes)
        {
            Debug.LogWarning(roomID + " normal task handle timeout. Releasing stuck task lock.");

            isBeingHandled = false;
            handleTimer = 0f;
        }
    }

    private void OnMouseDown()
    {
        Debug.Log("点击房间: " + roomID);

        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.TryCreateTask(this);
        }
    }

    public void CreateTask()
    {
        hasTask = true;
        isBeingHandled = false;
        waitingTime = 0f;
        handleTimer = 0f;
        heavySecondaryCallTriggered = false;

        GenerateEscalationThreshold();

        int randomValue = Random.Range(0, 100);

        if (randomValue < 50)
        {
            SetTaskType(TaskType.Light);
        }
        else if (randomValue < 80)
        {
            SetTaskType(TaskType.Medium);
        }
        else
        {
            SetTaskType(TaskType.Heavy);
        }

        Debug.Log("Room task created: " + roomID +
                  " | Type: " + currentTaskType +
                  " | Duration: " + taskDuration.ToString("F2") +
                  " | Escalation Threshold: " + escalationThreshold.ToString("F2"));
    }

    public void CreateTask(TaskType forcedType)
    {
        hasTask = true;
        isBeingHandled = false;
        waitingTime = 0f;
        handleTimer = 0f;
        heavySecondaryCallTriggered = false;

        GenerateEscalationThreshold();
        SetTaskType(forcedType);

        Debug.Log("Room task created: " + roomID +
                  " | Type: " + currentTaskType +
                  " | Duration: " + taskDuration.ToString("F2") +
                  " | Escalation Threshold: " + escalationThreshold.ToString("F2"));
    }

    private void SetTaskType(TaskType type)
    {
        currentTaskType = type;
        taskDuration = GetRandomTaskDuration(type);
        ApplyCurrentTaskBaseColor();
    }

    private float GetRandomTaskDuration(TaskType type)
    {
        float minMinutes = 0f;
        float maxMinutes = 0f;

        switch (type)
        {
            case TaskType.Light:
                minMinutes = 2f;
                maxMinutes = 5f;
                break;

            case TaskType.Medium:
                minMinutes = 6f;
                maxMinutes = 15f;
                break;

            case TaskType.Heavy:
                minMinutes = 12f;
                maxMinutes = 30f;
                break;
        }

        float randomMinutes = Random.Range(minMinutes, maxMinutes);
        return randomMinutes * 60f;
    }

    private void GenerateEscalationThreshold()
    {
        float randomMinutes = Random.Range(minEscalationMinutes, maxEscalationMinutes);
        escalationThreshold = randomMinutes * 60f;

        Debug.Log(roomID + " escalation threshold set to " +
                  randomMinutes.ToString("F1") + " simulated minutes");
    }

    private void CheckEscalation()
    {
        if (waitingTime < escalationThreshold) return;

        if (currentTaskType == TaskType.Light)
        {
            currentTaskType = TaskType.Medium;
            taskDuration = GetRandomTaskDuration(TaskType.Medium);

            waitingTime = 0f;
            GenerateEscalationThreshold();

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.RegisterLightToMediumEscalation();
            }

            Debug.Log("Secondary Call: " + roomID + " Light -> Medium");
        }
        else if (currentTaskType == TaskType.Medium)
        {
            currentTaskType = TaskType.Heavy;
            taskDuration = GetRandomTaskDuration(TaskType.Heavy);

            waitingTime = 0f;
            GenerateEscalationThreshold();

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.RegisterMediumToHeavyEscalation();
            }

            Debug.Log("Secondary Call: " + roomID + " Medium -> Heavy");
        }
        else if (currentTaskType == TaskType.Heavy)
        {
            if (!heavySecondaryCallTriggered)
            {
                heavySecondaryCallTriggered = true;

                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.RegisterHeavySecondaryCall();
                }

                Debug.Log("Secondary Call: " + roomID + " Heavy task waiting too long");
            }

            waitingTime = escalationThreshold;
        }

        ApplyCurrentTaskBaseColor();
    }

    public void CompleteTask()
    {
        float finalWaitingTime = waitingTime;

        hasTask = false;
        isBeingHandled = false;
        taskDuration = 0f;
        waitingTime = 0f;
        handleTimer = 0f;
        heavySecondaryCallTriggered = false;

        SetNormalColor();

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.RegisterTaskCompleted(finalWaitingTime);
        }

        Debug.Log("Room task completed: " + roomID);
    }

    private void ApplyCurrentTaskBaseColor()
    {
        if (roomRenderer == null) return;

        switch (currentTaskType)
        {
            case TaskType.Light:
                roomRenderer.color = lightTaskColor;
                break;

            case TaskType.Medium:
                roomRenderer.color = mediumTaskColor;
                break;

            case TaskType.Heavy:
                roomRenderer.color = heavyTaskColor;
                break;
        }
    }

    private void UpdateTaskColorByProgress()
    {
        if (roomRenderer == null) return;

        float progress = Mathf.Clamp01(waitingTime / escalationThreshold);

        if (currentTaskType == TaskType.Light)
        {
            roomRenderer.color = Color.Lerp(lightTaskColor, mediumTaskColor, progress);
        }
        else if (currentTaskType == TaskType.Medium)
        {
            roomRenderer.color = Color.Lerp(mediumTaskColor, heavyTaskColor, progress);
        }
        else if (currentTaskType == TaskType.Heavy)
        {
            roomRenderer.color = heavyTaskColor;
        }
    }

    public void SetNormalColor()
    {
        if (roomRenderer != null)
        {
            roomRenderer.color = normalColor;
        }
    }

    public List<Transform> GetReturnPath()
    {
        List<Transform> finalReturnPath = new List<Transform>();

        if (autoGenerateReturnPath)
        {
            finalReturnPath = new List<Transform>(goPath);
            finalReturnPath.Reverse();

            if (removeRoomPointFromReturn && finalReturnPath.Count > 0)
            {
                finalReturnPath.RemoveAt(0);
            }
        }
        else
        {
            finalReturnPath = new List<Transform>(manualReturnPath);
        }

        return finalReturnPath;
    }

    public void ResetRoomTask()
    {
        hasTask = false;
        isBeingHandled = false;
        waitingTime = 0f;
        taskDuration = 0f;
        handleTimer = 0f;
        heavySecondaryCallTriggered = false;

        SetNormalColor();
    }
}