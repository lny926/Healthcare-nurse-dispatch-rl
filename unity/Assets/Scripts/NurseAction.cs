using System.Collections.Generic;
using UnityEngine;

public class NurseAction : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("State")]
    public NurseState currentState = NurseState.Idle;

    [Header("Current Path")]
    public List<Transform> currentPath = new List<Transform>();
    private int pathIndex = 0;

    [Header("Speed Display")]
    public float currentActualSpeed = 0f;

    [Header("Personal Station Points")]
    public Transform stationPoint;
    public Transform exitPoint;

    [Header("Work Settings")]
    public float workDuration = 3f;
    private float workTimer = 0f;

    [Header("Shift Settings")]
    public bool enableShiftSystem = true;
    public float shiftLengthHours = 8f;
    public float workedHours = 0f;

    private bool shiftEndingRequested = false;

    [Header("Safety Recovery")]
    public float maxWorkingDuration = 120f;
    private float stateTimer = 0f;

    private List<Transform> returnPath = new List<Transform>();

    private RoomTask currentRoomTask;
    private RoomTask currentLocationRoom;

    private Vector3 lastPosition;
    private Vector3 initialPosition;

    private RoomRoutineTask currentRoutineTask;
    private bool isHandlingRoutineTask = false;

    [Header("Fatigue Settings")]
    [Range(0f, 1f)]
    public float fatigue = 0f;

    public float heavyTaskFatigueIncrease = 0.08f;
    public float fatigueRecoveryPerMinute = 0.002f;
    public float fatigueSpeedCoefficient = 0.5f;

    public bool IsIdle()
    {
        return currentState == NurseState.Idle;
    }

    public bool IsAvailable()
    {
        return currentState == NurseState.Idle
               && fatigue < 1f
               && !shiftEndingRequested;
    }

    void Start()
    {
        if (stationPoint != null)
        {
            initialPosition = stationPoint.position;
            transform.position = initialPosition;
        }
        else
        {
            initialPosition = transform.position;
        }

        lastPosition = transform.position;
        currentActualSpeed = CalculateCurrentMoveSpeed();
    }

    void Update()
    {
        currentActualSpeed = CalculateCurrentMoveSpeed();

        stateTimer += Time.deltaTime;

        UpdateShiftTimer();

        switch (currentState)
        {
            case NurseState.MovingToRoom:
            case NurseState.Returning:
                MoveAlongPath();
                break;

            case NurseState.Working:
                DoWork();
                break;

            case NurseState.Resting:
                HandleResting();
                break;

            case NurseState.Idle:
                break;
        }

        UpdateFatigueRecovery();
        CheckWorkingSafety();

        float frameDistance = Vector3.Distance(transform.position, lastPosition);
        if (frameDistance > 0f && StatsManager.Instance != null)
        {
            StatsManager.Instance.AddDistance(frameDistance);
        }

        lastPosition = transform.position;
    }

    private void SetState(NurseState newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    private float GetDeltaSimSeconds()
    {
        if (TimeManager.Instance != null)
        {
            return TimeManager.Instance.GetDeltaSimSeconds();
        }

        return Time.deltaTime;
    }

    public Vector3 GetInitialPosition()
    {
        return initialPosition;
    }

    public void SetTaskPath(List<Transform> goPath, List<Transform> backPath)
    {
        currentPath = new List<Transform>(goPath);
        returnPath = new List<Transform>(backPath);
        pathIndex = 0;
        SetState(NurseState.MovingToRoom);
    }

    public void AssignTask(RoomTask roomTask, List<Transform> goPath, List<Transform> backPath)
    {
        currentRoomTask = roomTask;

        List<Transform> finalGoPath = BuildDynamicGoPath(roomTask, goPath);
        List<Transform> finalBackPath = new List<Transform>(backPath);

        SetTaskPath(finalGoPath, finalBackPath);

        Debug.Log("护工开始前往房间: " + roomTask.roomID);
    }

    public void AssignRoutineTask(RoomRoutineTask routineTask)
    {
        currentRoutineTask = routineTask;
        isHandlingRoutineTask = true;

        if (currentRoutineTask != null)
        {
            currentRoutineTask.StartHandling();
        }

        List<Transform> routinePath = BuildRoutineMedicationPath(routineTask);

        currentPath = routinePath;
        returnPath.Clear();
        pathIndex = 0;
        SetState(NurseState.MovingToRoom);

        Debug.Log("护工开始处理 Medication Routine Task: " + routineTask.roomID);
    }

    private List<Transform> BuildRoutineMedicationPath(RoomRoutineTask routineTask)
    {
        List<Transform> path = new List<Transform>();

        if (currentLocationRoom != null &&
            currentLocationRoom.goPath != null &&
            currentLocationRoom.goPath.Count > 0)
        {
            List<Transform> roomPath = currentLocationRoom.goPath;

            for (int i = roomPath.Count - 2; i >= 0; i--)
            {
                if (roomPath[i] == null) continue;
                if (roomPath[i].name == "BaseCenter") continue;

                path.Add(roomPath[i]);
            }
        }

        bool isAtStation = currentLocationRoom == null;

        if (!isAtStation)
        {
            if (exitPoint != null) path.Add(exitPoint);
            if (stationPoint != null) path.Add(stationPoint);
        }
        else
        {
            if (stationPoint != null) path.Add(stationPoint);
        }

        if (exitPoint != null)
        {
            path.Add(exitPoint);
        }

        if (routineTask != null && routineTask.goPath != null)
        {
            foreach (Transform point in routineTask.goPath)
            {
                if (point == null) continue;
                if (point.name == "BaseCenter") continue;

                path.Add(point);
            }
        }

        return path;
    }

    private List<Transform> BuildDynamicGoPath(RoomTask targetRoom, List<Transform> targetGoPath)
    {
        List<Transform> path = new List<Transform>();

        if (currentLocationRoom == null ||
            currentLocationRoom.goPath == null ||
            currentLocationRoom.goPath.Count == 0)
        {
            if (exitPoint != null)
            {
                path.Add(exitPoint);
            }

            AddPathSkippingBaseCenter(path, targetGoPath);
            return path;
        }

        List<Transform> currentRoomPath = currentLocationRoom.goPath;
        List<Transform> targetRoomPath = targetGoPath;

        int lastCommonIndexCurrent = -1;
        int lastCommonIndexTarget = -1;

        for (int i = 0; i < currentRoomPath.Count; i++)
        {
            for (int j = 0; j < targetRoomPath.Count; j++)
            {
                if (currentRoomPath[i] == targetRoomPath[j])
                {
                    lastCommonIndexCurrent = i;
                    lastCommonIndexTarget = j;
                }
            }
        }

        if (lastCommonIndexCurrent == -1 || lastCommonIndexTarget == -1)
        {
            Debug.LogWarning("No common waypoint found. Fallback to target goPath.");

            if (exitPoint != null)
            {
                path.Add(exitPoint);
            }

            path.AddRange(targetGoPath);
            return path;
        }

        for (int i = currentRoomPath.Count - 2; i >= lastCommonIndexCurrent; i--)
        {
            path.Add(currentRoomPath[i]);
        }

        for (int i = lastCommonIndexTarget + 1; i < targetRoomPath.Count; i++)
        {
            path.Add(targetRoomPath[i]);
        }

        return path;
    }

    private void AddPathSkippingBaseCenter(List<Transform> resultPath, List<Transform> sourcePath)
    {
        foreach (Transform point in sourcePath)
        {
            if (point == null) continue;
            if (point.name == "BaseCenter") continue;

            resultPath.Add(point);
        }
    }

    private List<Transform> BuildReturnToStationPath()
    {
        List<Transform> path = new List<Transform>();

        if (currentLocationRoom != null &&
            currentLocationRoom.goPath != null &&
            currentLocationRoom.goPath.Count > 0)
        {
            List<Transform> roomPath = currentLocationRoom.goPath;

            for (int i = roomPath.Count - 2; i >= 0; i--)
            {
                if (roomPath[i] == null) continue;
                if (roomPath[i].name == "BaseCenter") continue;

                path.Add(roomPath[i]);
            }
        }

        if (exitPoint != null)
        {
            path.Add(exitPoint);
        }

        if (stationPoint != null)
        {
            path.Add(stationPoint);
        }
        else
        {
            GameObject fallback = new GameObject(name + "_FallbackStationPoint");
            fallback.transform.position = initialPosition;
            path.Add(fallback.transform);
        }

        return path;
    }

    private float CalculateCurrentMoveSpeed()
    {
        float speedMultiplier = 1f - fatigue * fatigueSpeedCoefficient;
        speedMultiplier = Mathf.Clamp(speedMultiplier, 0.2f, 1f);

        return moveSpeed * speedMultiplier;
    }

    private float GetCurrentMoveSpeed()
    {
        currentActualSpeed = CalculateCurrentMoveSpeed();
        return currentActualSpeed;
    }

    private void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning(name + " has empty path in state: " + currentState);
            ReturnToStationSafely();
            return;
        }

        if (pathIndex < 0 || pathIndex >= currentPath.Count)
        {
            Debug.LogWarning(name + " invalid pathIndex: " + pathIndex);
            ReturnToStationSafely();
            return;
        }

        if (currentPath[pathIndex] == null)
        {
            Debug.LogWarning(name + " null waypoint detected.");
            ReturnToStationSafely();
            return;
        }

        Transform targetPoint = currentPath[pathIndex];

        float moveDelta;

        if (ExperimentManager.IsExperimentMode)
        {
            float simulatedMinutes = GetDeltaSimSeconds() / 60f;
            moveDelta = GetCurrentMoveSpeed() * simulatedMinutes;
        }
        else
        {
            moveDelta = GetCurrentMoveSpeed() * Time.deltaTime;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveDelta
        );

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
        {
            transform.position = targetPoint.position;
            pathIndex++;

            if (pathIndex >= currentPath.Count)
            {
                if (currentState == NurseState.MovingToRoom)
                {
                    SetState(NurseState.Working);

                    if (isHandlingRoutineTask && currentRoutineTask != null)
                    {
                        if (StatsManager.Instance != null)
                        {
                            StatsManager.Instance.RegisterTaskStarted();
                        }

                        workTimer = currentRoutineTask.medicationDuration;

                        Debug.Log("到达房间，开始 Medication: " + currentRoutineTask.roomID +
                                  " | 工作时长: " + workTimer);
                    }
                    else if (currentRoomTask != null)
                    {
                        currentRoomTask.isBeingHandled = true;

                        if (StatsManager.Instance != null)
                        {
                            StatsManager.Instance.RegisterTaskStarted();
                        }

                        workTimer = currentRoomTask.taskDuration;

                        Debug.Log("到达房间，开始工作: " + currentRoomTask.roomID +
                                  " | 类型: " + currentRoomTask.currentTaskType +
                                  " | 工作时长: " + workTimer);
                    }
                    else
                    {
                        Debug.LogWarning(name + " arrived but has no valid task. Returning to station.");
                        ReturnToStationSafely();
                    }
                }
                else if (currentState == NurseState.Returning)
                {
                    Debug.Log("护工已返回自己的护士站位置");

                    currentPath.Clear();
                    returnPath.Clear();
                    pathIndex = 0;

                    currentLocationRoom = null;
                    currentRoomTask = null;
                    currentRoutineTask = null;
                    isHandlingRoutineTask = false;

                    if (shiftEndingRequested)
                    {
                        RefreshAfterShift();
                    }
                    else if (fatigue >= 1f)
                    {
                        SetState(NurseState.Resting);
                        Debug.Log(name + " is resting due to fatigue.");
                    }
                    else
                    {
                        SetState(NurseState.Idle);
                        Debug.Log(name + " is idle at nurse station.");

                        if (TaskManager.Instance != null)
                        {
                            TaskManager.Instance.TryAssignNextTask();
                        }
                    }
                }
            }
        }
    }

    private void DoWork()
    {
        if (currentRoomTask == null && currentRoutineTask == null)
        {
            Debug.LogWarning(name + " is Working but has no valid task. Returning to station.");
            ReturnToStationSafely();
            return;
        }

        workTimer -= GetDeltaSimSeconds();

        if (workTimer <= 0f)
        {
            if (isHandlingRoutineTask && currentRoutineTask != null)
            {
                Debug.Log("完成 Medication Routine Task: " + currentRoutineTask.roomID);

                currentRoutineTask.CompleteMedicationTask();

                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.RegisterRoutineTaskCompleted();
                }

                RoomTask roomTask = currentRoutineTask.GetComponent<RoomTask>();
                if (roomTask != null)
                {
                    currentLocationRoom = roomTask;
                }

                currentRoutineTask = null;
                isHandlingRoutineTask = false;

                AfterTaskFinished();
                return;
            }

            if (currentRoomTask != null)
            {
                if (currentRoomTask.currentTaskType == TaskType.Heavy)
                {
                    AddFatigue(heavyTaskFatigueIncrease);
                }

                Debug.Log("完成普通护理任务: " + currentRoomTask.roomID);

                currentLocationRoom = currentRoomTask;
                currentRoomTask.CompleteTask();
                currentRoomTask = null;
            }

            AfterTaskFinished();
        }
    }

    private void AfterTaskFinished()
    {
        if (shiftEndingRequested)
        {
            currentPath = BuildReturnToStationPath();
            pathIndex = 0;
            SetState(NurseState.Returning);

            Debug.Log(name + " completed current task and is returning for shift change.");
            return;
        }

        if (fatigue >= 1f)
        {
            currentPath = BuildReturnToStationPath();
            pathIndex = 0;
            SetState(NurseState.Returning);

            Debug.Log(name + " is exhausted, returning to station to rest.");
            return;
        }

        if (TaskManager.Instance != null &&
            (TaskManager.Instance.GetPendingTaskCount() > 0 ||
             TaskManager.Instance.GetPendingRoutineTaskCount() > 0))
        {
            SetState(NurseState.Idle);
            currentPath.Clear();
            returnPath.Clear();
            pathIndex = 0;

            Debug.Log("任务完成，尝试从当前位置继续接任务。");

            TaskManager.Instance.TryAssignNextTask();

            if (currentState == NurseState.Idle)
            {
                currentPath = BuildReturnToStationPath();
                pathIndex = 0;
                SetState(NurseState.Returning);

                Debug.Log("没有可立即分配的任务，护士返回护士站。");
            }

            return;
        }

        currentPath = BuildReturnToStationPath();
        pathIndex = 0;
        SetState(NurseState.Returning);

        Debug.Log("任务完成，当前没有等待任务，护士返回自己的站位点。");
    }

    private void AddFatigue(float amount)
    {
        fatigue += amount;
        fatigue = Mathf.Clamp01(fatigue);

        Debug.Log(name + " fatigue increased to: " + fatigue.ToString("F2"));

        if (fatigue >= 1f)
        {
            Debug.Log(name + " is exhausted! Will return to station after current task.");
        }
    }

    public void ResetNurse()
    {
        if (stationPoint != null)
        {
            initialPosition = stationPoint.position;
            transform.position = initialPosition;
        }
        else
        {
            transform.position = initialPosition;
        }

        SetState(NurseState.Idle);
        currentPath.Clear();
        returnPath.Clear();

        pathIndex = 0;
        workTimer = 0f;

        SafeReleaseCurrentTasks();

        currentLocationRoom = null;
        currentRoomTask = null;
        currentRoutineTask = null;
        isHandlingRoutineTask = false;

        workedHours = 0f;
        shiftEndingRequested = false;

        fatigue = 0f;
        lastPosition = transform.position;
        currentActualSpeed = CalculateCurrentMoveSpeed();

        Debug.Log("Nurse reset to station position.");
    }

    public void SetInitialPosition(Vector3 pos)
    {
        initialPosition = pos;
        transform.position = pos;
        lastPosition = pos;
        currentActualSpeed = CalculateCurrentMoveSpeed();
    }

    private void UpdateFatigueRecovery()
    {
        bool shouldRecover = false;

        if (currentState == NurseState.Idle)
        {
            shouldRecover = true;
        }

        if (currentState == NurseState.Working &&
            currentRoomTask != null &&
            currentRoomTask.currentTaskType == TaskType.Light)
        {
            shouldRecover = true;
        }

        if (!shouldRecover) return;

        float minutes = GetFatigueMinutesThisFrame();

        fatigue -= fatigueRecoveryPerMinute * minutes;
        fatigue = Mathf.Clamp01(fatigue);
    }

    private float GetFatigueMinutesThisFrame()
    {
        return GetDeltaSimSeconds() / 60f;
    }

    private void UpdateShiftTimer()
    {
        if (!enableShiftSystem) return;

        float deltaSeconds = GetDeltaSimSeconds();

        if (deltaSeconds <= 0f) return;

        workedHours += deltaSeconds / 3600f;

        if (workedHours >= shiftLengthHours && !shiftEndingRequested)
        {
            RequestShiftEnd();
        }
    }

    private void RequestShiftEnd()
    {
        if (shiftEndingRequested)
        {
            return;
        }

        shiftEndingRequested = true;

        if (currentState == NurseState.Idle)
        {
            StartReturnToStationForShiftEnd();
            return;
        }

        if (currentState == NurseState.Resting)
        {
            RefreshAfterShift();
            return;
        }

        Debug.Log(name + " shift end requested. Current state: " + currentState);
    }

    private void StartReturnToStationForShiftEnd()
    {
        currentPath = BuildReturnToStationPath();
        pathIndex = 0;
        SetState(NurseState.Returning);

        Debug.Log(name + " shift ended, returning to station for refresh.");
    }

    private void RefreshAfterShift()
    {
        SafeReleaseCurrentTasks();

        workedHours -= shiftLengthHours;

        if (workedHours < 0f)
        {
            workedHours = 0f;
        }

        shiftEndingRequested = false;
        fatigue = 0f;

        currentPath.Clear();
        returnPath.Clear();
        pathIndex = 0;
        workTimer = 0f;

        currentLocationRoom = null;
        currentRoomTask = null;
        currentRoutineTask = null;
        isHandlingRoutineTask = false;

        SetState(NurseState.Idle);
        currentActualSpeed = CalculateCurrentMoveSpeed();

        Debug.Log(name + " completed shift change and refreshed status. WorkedHours left: " + workedHours.ToString("F2"));

        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.TryAssignNextTask();
        }
    }

    private void HandleResting()
    {
        if (shiftEndingRequested)
        {
            RefreshAfterShift();
            return;
        }

        float minutes = GetFatigueMinutesThisFrame();

        fatigue -= fatigueRecoveryPerMinute * minutes;
        fatigue = Mathf.Clamp01(fatigue);

        if (fatigue <= 0.5f)
        {
            SetState(NurseState.Idle);
            Debug.Log(name + " has recovered and is back to work.");

            if (TaskManager.Instance != null)
            {
                TaskManager.Instance.TryAssignNextTask();
            }
        }
    }

    private void CheckWorkingSafety()
    {
        if (currentState != NurseState.Working) return;

        if (stateTimer > maxWorkingDuration)
        {
            Debug.LogWarning(name + " has been Working for too long. Force returning to station.");
            ReturnToStationSafely();
        }
    }

    private void ReturnToStationSafely()
    {
        currentPath = BuildReturnToStationPath();
        pathIndex = 0;

        if (currentPath == null || currentPath.Count == 0)
        {
            if (stationPoint != null)
            {
                transform.position = stationPoint.position;
            }
            else
            {
                transform.position = initialPosition;
            }

            currentLocationRoom = null;
            currentRoomTask = null;
            currentRoutineTask = null;
            isHandlingRoutineTask = false;

            if (shiftEndingRequested)
            {
                RefreshAfterShift();
            }
            else
            {
                SetState(NurseState.Idle);
            }

            return;
        }

        SetState(NurseState.Returning);
    }

    private void SafeReleaseCurrentTasks()
    {
        if (currentRoomTask != null)
        {
            currentRoomTask.isBeingHandled = false;
        }

        if (currentRoutineTask != null)
        {
            currentRoutineTask.isBeingHandled = false;
        }
    }
}