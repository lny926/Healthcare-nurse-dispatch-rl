using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    [Header("Nurses In Scene")]
    public List<NurseAction> nurses = new List<NurseAction>();

    [Header("Dispatch Mode")]
    public DispatchMode currentMode = DispatchMode.FCFS;
    public DispatchMode pendingMode = DispatchMode.FCFS;
    public DispatchMode defaultMode = DispatchMode.FCFS;

    [Header("All Rooms In Scene")]
    public List<RoomTask> allRooms = new List<RoomTask>();

    [Header("Task Generator")]
    public TaskGenerator taskGenerator;

    [Header("AI Score Weights")]
    public float distanceWeight = 1.0f;
    public float fatigueWeight = 20.0f;
    public float priorityWeight = 10.0f;
    public float waitingTimeWeight = 1.0f;

    [Header("RL Agent")]
    public NurseDispatchAgent nurseDispatchAgent;

    // 待处理任务列表
    private List<RoomTask> pendingTasks = new List<RoomTask>();

    // Routine 任务队列（Medication）
    private List<RoomRoutineTask> pendingRoutineTasks = new List<RoomRoutineTask>();

    private void Awake()
    {
        Instance = this;
        pendingMode = currentMode;
    }

    void Update()
    {
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.UpdateAverageFatigue(nurses);
        }
    }

    // 手动点击房间时调用：随机创建任务
    public void TryCreateTask(RoomTask roomTask)
    {
        if (roomTask.hasTask)
        {
            Debug.Log("This room already has a task: " + roomTask.roomID);
            return;
        }

        if (roomTask.goPath == null || roomTask.goPath.Count == 0)
        {
            Debug.LogError("Room " + roomTask.roomID + " has empty goPath!");
            return;
        }

        roomTask.CreateTask();
        pendingTasks.Add(roomTask);

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.RegisterTaskCreated(roomTask.currentTaskType);
        }

        Debug.Log("Task added to pending list: " + roomTask.roomID +
                  " | Type: " + roomTask.currentTaskType +
                  " | Pending Count: " + pendingTasks.Count);

        TryAssignNextTask();
    }

    public void AddRoutineTask(RoomRoutineTask routineTask)
    {
        if (routineTask == null) return;

        if (pendingRoutineTasks.Contains(routineTask))
        {
            Debug.Log("Routine task already in queue: " + routineTask.roomID);
            return;
        }

        pendingRoutineTasks.Add(routineTask);

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.RegisterRoutineTaskCreated();
        }

        Debug.Log("Routine task added to queue: " + routineTask.roomID +
                  " | Pending Routine Count: " + pendingRoutineTasks.Count);

        TryAssignNextTask();
    }

    public int GetPendingRoutineTaskCount()
    {
        return pendingRoutineTasks.Count;
    }

    // 自动生成器调用：创建指定类型任务
    public void TryCreateTask(RoomTask roomTask, TaskType forcedType)
    {
        if (roomTask.hasTask)
        {
            Debug.Log("This room already has a task: " + roomTask.roomID);
            return;
        }

        if (roomTask.goPath == null || roomTask.goPath.Count == 0)
        {
            Debug.LogError("Room " + roomTask.roomID + " has empty goPath!");
            return;
        }

        roomTask.CreateTask(forcedType);
        pendingTasks.Add(roomTask);

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.RegisterTaskCreated(forcedType);
        }

        Debug.Log("Task added to pending list: " + roomTask.roomID +
                  " | Type: " + forcedType +
                  " | Pending Count: " + pendingTasks.Count);

        TryAssignNextTask();
    }

    // UI按钮调用：请求切换策略
    public void RequestModeChange(int modeIndex)
    {
        pendingMode = (DispatchMode)modeIndex;
        Debug.Log("Requested mode change to: " + pendingMode + " (will apply on next assignment)");
    }

    // 只有在准备分配新任务时，才正式应用待切换策略
    private void ApplyPendingModeIfNeeded()
    {
        if (currentMode != pendingMode)
        {
            currentMode = pendingMode;
            Debug.Log("Dispatch mode switched to: " + currentMode);
        }
    }

    // 获取第一个空闲护士（给 FCFS / Priority 使用）
    private NurseAction GetFirstIdleNurse()
    {
        foreach (NurseAction n in nurses)
        {
            if (n != null && n.IsAvailable())
            {
                return n;
            }
        }

        return null;
    }

    // 是否存在空闲护士
    private bool HasAnyIdleNurse()
    {
        return GetFirstIdleNurse() != null;
    }

    // 任务分配入口
    public void TryAssignNextTask()
    {
        if (nurses == null || nurses.Count == 0)
        {
            Debug.LogError("TaskManager has no nurses assigned!");
            return;
        }

        if (!HasAnyIdleNurse())
        {
            return;
        }

        ApplyPendingModeIfNeeded();

        if (pendingTasks.Count == 0)
        {
            Debug.Log("No normal pending tasks. Trying routine tasks...");
            TryAssignNextRoutineTask();
            return;
        }

        NurseAction selectedNurse = null;
        RoomTask selectedTask = null;

        switch (currentMode)
        {
            case DispatchMode.FCFS:
                selectedTask = SelectFCFS();
                selectedNurse = GetFirstIdleNurse();
                break;

            case DispatchMode.PriorityFirst:
                selectedTask = SelectPriorityFirst();
                selectedNurse = GetFirstIdleNurse();
                break;

            case DispatchMode.ShortestDistance:
                SelectBestShortestDistancePair(out selectedNurse, out selectedTask);
                break;

            case DispatchMode.AIScore:
                SelectBestAIScorePair(out selectedNurse, out selectedTask);
                break;

            case DispatchMode.RL:
                SelectRLPair(out selectedNurse, out selectedTask);
                break;

            default:
                selectedTask = SelectFCFS();
                selectedNurse = GetFirstIdleNurse();
                break;
        }

        if (selectedNurse == null || selectedTask == null)
        {
            Debug.LogWarning("No valid nurse-task pair found.");
            return;
        }

        pendingTasks.Remove(selectedTask);

        List<Transform> returnPath = selectedTask.GetReturnPath();
        selectedNurse.AssignTask(selectedTask, selectedTask.goPath, returnPath);

        Debug.Log("Assigned task: " + selectedTask.roomID +
                  " | Type: " + selectedTask.currentTaskType +
                  " | Nurse: " + selectedNurse.name +
                  " | Mode: " + currentMode +
                  " | Remaining Pending: " + pendingTasks.Count);
    }

    // FCFS：取最早进入 pending 的任务
    private RoomTask SelectFCFS()
    {
        if (pendingTasks.Count == 0) return null;
        return pendingTasks[0];
    }

    // Priority：Heavy > Medium > Light
    private RoomTask SelectPriorityFirst()
    {
        if (pendingTasks.Count == 0) return null;

        RoomTask bestTask = null;
        int bestPriority = -1;

        foreach (RoomTask task in pendingTasks)
        {
            if (task == null) continue;

            int priority = GetTaskPriority(task.currentTaskType);

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestTask = task;
            }
        }

        return bestTask;
    }

    // Shortest Distance：在“所有空闲护士 - 所有待处理任务”中，找最近的一对
    private void SelectBestShortestDistancePair(out NurseAction bestNurse, out RoomTask bestTask)
    {
        bestNurse = null;
        bestTask = null;

        float bestDistance = float.MaxValue;

        foreach (NurseAction nurse in nurses)
        {
            if (nurse == null || !nurse.IsAvailable()) continue;

            foreach (RoomTask task in pendingTasks)
            {
                if (task == null || task.goPath == null || task.goPath.Count == 0) continue;

                Transform roomTarget = task.goPath[task.goPath.Count - 1];
                float distance = Vector3.Distance(nurse.transform.position, roomTarget.position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNurse = nurse;
                    bestTask = task;
                }
            }
        }
    }

    // AI Score：综合距离、疲劳、任务优先级、等待时间
    private void SelectBestAIScorePair(out NurseAction bestNurse, out RoomTask bestTask)
    {
        bestNurse = null;
        bestTask = null;

        float bestScore = float.MaxValue;

        foreach (NurseAction nurse in nurses)
        {
            if (nurse == null || !nurse.IsAvailable()) continue;

            foreach (RoomTask task in pendingTasks)
            {
                if (task == null || task.goPath == null || task.goPath.Count == 0) continue;

                float score = CalculateAIScore(nurse, task);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestNurse = nurse;
                    bestTask = task;
                }
            }
        }
    }

    private void SelectRLPair(out NurseAction bestNurse, out RoomTask bestTask)
    {
        bestNurse = null;
        bestTask = null;

        if (pendingTasks.Count == 0) return;

        // 先让 RL 只负责“选护士”
        // 任务本身先按 FCFS 选第一个
        bestTask = SelectFCFS();

        if (nurseDispatchAgent == null)
        {
            Debug.LogWarning("No NurseDispatchAgent assigned. Fallback to shortest distance.");
            SelectBestShortestDistancePair(out bestNurse, out bestTask);
            return;
        }

        // 告诉 Agent：当前要处理的是哪个任务
        nurseDispatchAgent.SetCurrentDecisionTask(bestTask);

        // 让 Agent 根据当前 observation 做一次决策
        nurseDispatchAgent.RequestDecision();

        int nurseIndex = nurseDispatchAgent.lastSelectedNurseIndex;

        if (nurseIndex < 0 || nurseIndex >= nurses.Count)
        {
            Debug.LogWarning("RL selected invalid nurse index: " + nurseIndex);

            nurseDispatchAgent.GiveDispatchReward(false, null, bestTask);

            return;
        }

        NurseAction selected = nurses[nurseIndex];

        if (selected == null || !selected.IsAvailable())
        {
            Debug.LogWarning("RL selected unavailable nurse: " + nurseIndex);

            nurseDispatchAgent.GiveDispatchReward(false, selected, bestTask);

            return;
        }

        bestNurse = selected;

        nurseDispatchAgent.GiveDispatchReward(true, selected, bestTask);

        Debug.Log("RL selected task: " + bestTask.roomID +
                  " | Nurse: " + bestNurse.name +
                  " | Index: " + nurseIndex);
    }

    private float CalculateAIScore(NurseAction nurse, RoomTask task)
    {
        Transform roomTarget = task.goPath[task.goPath.Count - 1];

        float distance = Vector3.Distance(nurse.transform.position, roomTarget.position);

        float fatigue = nurse.fatigue;

        int priority = GetTaskPriority(task.currentTaskType);

        float waitingTime = task.waitingTime;

        // score 越低越优
        float score =
            distanceWeight * distance +
            fatigueWeight * fatigue -
            priorityWeight * priority -
            waitingTimeWeight * waitingTime;

        return score;
    }

    private int GetTaskPriority(TaskType type)
    {
        switch (type)
        {
            case TaskType.Light:
                return 1;
            case TaskType.Medium:
                return 2;
            case TaskType.Heavy:
                return 3;
            default:
                return 0;
        }
    }

    private RoomRoutineTask SelectNextRoutineTask()
    {
        if (pendingRoutineTasks.Count == 0) return null;

        foreach (RoomRoutineTask routineTask in pendingRoutineTasks)
        {
            if (routineTask == null) continue;

            if (!routineTask.IsAvailableForRoutine()) continue;

            if (IsRoutineRoomBlocked(routineTask)) continue;

            return routineTask;
        }

        return null;
    }

    private bool IsRoutineRoomBlocked(RoomRoutineTask routineTask)
    {
        if (routineTask == null) return true;

        RoomTask roomTask = routineTask.linkedRoomTask;

        // 兜底：如果没手动拖，再尝试同物体获取
        if (roomTask == null)
        {
            roomTask = routineTask.GetComponent<RoomTask>();
        }

        // 如果仍然找不到 RoomTask，先不阻塞
        if (roomTask == null)
        {
            Debug.LogWarning("No linked RoomTask found for routine room: " + routineTask.roomID);
            return false;
        }

        // 只要这个房间存在普通任务，就不要派 routine 进去
        // 包括：等待中 / 护士路上 / 正在处理
        if (roomTask.hasTask)
        {
            Debug.Log("Routine task blocked because normal task exists in room: " + routineTask.roomID);
            return true;
        }

        return false;
    }

    private void TryAssignNextRoutineTask()
    {
        if (pendingRoutineTasks.Count == 0) return;

        NurseAction idleNurse = GetFirstIdleNurse();

        if (idleNurse == null)
        {
            return;
        }

        RoomRoutineTask routineTask = SelectNextRoutineTask();

        if (routineTask == null)
        {
            Debug.Log("No available routine task can be assigned right now.");
            return;
        }

        pendingRoutineTasks.Remove(routineTask);

        idleNurse.AssignRoutineTask(routineTask);

        Debug.Log("Assigned routine medication task: " + routineTask.roomID +
                  " | Nurse: " + idleNurse.name +
                  " | Remaining Routine Tasks: " + pendingRoutineTasks.Count);
    }

    // 给外部调试用：查看当前待处理任务数
    public int GetPendingTaskCount()
    {
        return pendingTasks.Count;
    }

    // Reset 整个 simulation
    public void ResetSimulation()
    {
        Debug.Log("Reset Simulation");

        // 1. 清空待处理任务
        pendingTasks.Clear();

        // 清空 Routine 任务
        pendingRoutineTasks.Clear();

        // 2. 重置所有房间
        foreach (RoomTask room in allRooms)
        {
            if (room != null)
            {
                room.ResetRoomTask();
            }
        }

        // 3. 重置所有护工
        foreach (NurseAction n in nurses)
        {
            if (n != null)
            {
                n.ResetNurse();
            }
        }

        // 4. 重置时间到 00:00
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.ResetTimeToMidnight();
        }

        // 5. 重置统计
        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.ResetStats();
        }

        // 6. 重置策略
        currentMode = defaultMode;
        pendingMode = defaultMode;

        // 7. 重置任务生成器
        if (taskGenerator != null)
        {
            taskGenerator.ResetGenerator();
        }

        Debug.Log("Simulation reset complete.");

        // 8. 重置 Routine Task
        if (RoutineTaskManager.Instance != null)
        {
            RoutineTaskManager.Instance.ResetAllRoutineRooms();
        }
    }
}

