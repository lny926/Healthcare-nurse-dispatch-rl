using UnityEngine;
using System.Collections.Generic;

public class RoomRoutineTask : MonoBehaviour
{
    [Header("Room Info")]
    public string roomID;

    [Header("Linked Normal Room Task")]
    public RoomTask linkedRoomTask;

    [Header("Medication UI")]
    public SpriteRenderer medicationRenderer;
    public Color inactiveColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);
    public Color activeColor = Color.cyan;

    [Header("Medication Task State")]
    public bool hasMedicationTask = false;
    public bool isBeingHandled = false;

    [Header("Medication Duration")]
    public float medicationDuration = 0f;

    [Header("Path Settings")]
    public List<Transform> goPath = new List<Transform>();

    [Header("Safety Timeout")]
    public float maxHandleTimeoutMinutes = 30f;

    private float handleTimer = 0f;

    private void Start()
    {
        SetMedicationInactive();
    }

    private void Update()
    {
        CheckHandleTimeout();
    }

    public void ActivateMedicationTask()
    {
        if (hasMedicationTask)
        {
            Debug.Log("Medication task already exists in room: " + roomID);
            return;
        }

        hasMedicationTask = true;
        isBeingHandled = false;

        handleTimer = 0f;

        medicationDuration = GetRandomMedicationDuration();

        SetMedicationActive();

        Debug.Log("Medication task activated: " + roomID +
                  " | Duration: " + medicationDuration.ToString("F2"));
    }

    public void StartHandling()
    {
        isBeingHandled = true;
        handleTimer = 0f;
    }

    public void CompleteMedicationTask()
    {
        hasMedicationTask = false;
        isBeingHandled = false;

        handleTimer = 0f;

        medicationDuration = 0f;

        SetMedicationInactive();

        Debug.Log("Medication task completed: " + roomID);
    }

    private void CheckHandleTimeout()
    {
        if (!hasMedicationTask)
        {
            handleTimer = 0f;
            return;
        }

        if (!isBeingHandled)
        {
            handleTimer = 0f;
            return;
        }

        float simMinutes = 0f;

        if (TimeManager.Instance != null)
        {
            simMinutes = TimeManager.Instance.GetDeltaSimSeconds() / 60f;
        }
        else
        {
            simMinutes = Time.deltaTime / 60f;
        }

        handleTimer += simMinutes;

        // 超时自动解锁
        if (handleTimer >= maxHandleTimeoutMinutes)
        {
            Debug.LogWarning(
                roomID +
                " medication task timeout. Releasing stuck task lock."
            );

            isBeingHandled = false;
            handleTimer = 0f;
        }
    }

    private float GetRandomMedicationDuration()
    {
        float randomMinutes = Random.Range(2f, 5f);

        return randomMinutes * 60f;
    }

    private void SetMedicationActive()
    {
        if (medicationRenderer != null)
        {
            medicationRenderer.color = activeColor;
        }
    }

    private void SetMedicationInactive()
    {
        if (medicationRenderer != null)
        {
            medicationRenderer.color = inactiveColor;
        }
    }

    public bool IsAvailableForRoutine()
    {
        return hasMedicationTask && !isBeingHandled;
    }

    public void ResetRoutineTask()
    {
        hasMedicationTask = false;
        isBeingHandled = false;

        handleTimer = 0f;

        medicationDuration = 0f;

        SetMedicationInactive();
    }
}