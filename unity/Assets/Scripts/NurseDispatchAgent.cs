using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class NurseDispatchAgent : Agent
{
    public TaskManager taskManager;

    [Header("RL Output")]
    public int lastSelectedNurseIndex = 0;

    [Header("Current Task Info")]
    public RoomTask currentDecisionTask;

    public override void Initialize()
    {
        if (taskManager == null)
        {
            taskManager = TaskManager.Instance;
        }
    }

    public void SetCurrentDecisionTask(RoomTask task)
    {
        currentDecisionTask = task;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (taskManager == null || taskManager.nurses == null)
        {
            sensor.AddObservation(0f);
            return;
        }

        Vector3 taskPosition = Vector3.zero;

        if (currentDecisionTask != null &&
            currentDecisionTask.goPath != null &&
            currentDecisionTask.goPath.Count > 0)
        {
            taskPosition = currentDecisionTask.goPath[currentDecisionTask.goPath.Count - 1].position;
        }

        foreach (NurseAction nurse in taskManager.nurses)
        {
            if (nurse == null)
            {
                sensor.AddObservation(0f); // available
                sensor.AddObservation(1f); // fatigue
                sensor.AddObservation(1f); // distance
                continue;
            }

            float available = nurse.IsAvailable() ? 1f : 0f;
            float fatigue = nurse.fatigue;

            float distance = 0f;
            if (currentDecisionTask != null)
            {
                distance = Vector3.Distance(nurse.transform.position, taskPosition);
            }

            // 简单归一化，避免数值太大
            float normalizedDistance = Mathf.Clamp01(distance / 30f);

            sensor.AddObservation(available);
            sensor.AddObservation(fatigue);
            sensor.AddObservation(normalizedDistance);
        }

        // 当前任务信息
        if (currentDecisionTask != null)
        {
            sensor.AddObservation(GetTaskTypeValue(currentDecisionTask.currentTaskType));
            sensor.AddObservation(Mathf.Clamp01(currentDecisionTask.waitingTime / 3600f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int nurseIndex = actions.DiscreteActions[0];

        lastSelectedNurseIndex = nurseIndex;

        AddReward(0.01f);
    }

    public void GiveDispatchReward(bool success, NurseAction nurse, RoomTask task)
    {
        if (!success)
        {
            AddReward(-1f);
            return;
        }

        float reward = 0.2f;

        if (nurse != null && task != null && task.goPath != null && task.goPath.Count > 0)
        {
            Transform roomTarget = task.goPath[task.goPath.Count - 1];

            float distance = Vector3.Distance(nurse.transform.position, roomTarget.position);
            float normalizedDistance = Mathf.Clamp01(distance / 30f);

            float fatigue = nurse.fatigue;

            reward += 0.3f * (1f - normalizedDistance);
            reward += 0.3f * (1f - fatigue);
        }

        AddReward(reward);
    }

    private float GetTaskTypeValue(TaskType type)
    {
        switch (type)
        {
            case TaskType.Light:
                return 0.33f;
            case TaskType.Medium:
                return 0.66f;
            case TaskType.Heavy:
                return 1.0f;
            default:
                return 0f;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
    }
}