using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("Time Settings")]
    public float timeScale = 60f;   // 1 real second = 60 simulated seconds

    private float currentTime = 0f;              // seconds within current day
    private float totalSimulatedSeconds = 0f;    // accumulated simulated seconds

    public float DeltaSimSeconds { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        DeltaSimSeconds = Time.deltaTime * timeScale;

        currentTime += DeltaSimSeconds;
        totalSimulatedSeconds += DeltaSimSeconds;

        // Support large timeScale crossing more than one day
        while (currentTime >= 86400f)
        {
            currentTime -= 86400f;
        }
    }

    public float GetDeltaSimSeconds()
    {
        return DeltaSimSeconds;
    }

    public int GetHour()
    {
        return Mathf.FloorToInt(currentTime / 3600f);
    }

    public int GetMinute()
    {
        return Mathf.FloorToInt((currentTime % 3600f) / 60f);
    }

    public string GetFormattedTime()
    {
        int day = GetDayCount();
        return "Day " + day + " " + GetHour().ToString("00") + ":" + GetMinute().ToString("00");
    }

    public int GetDayCount()
    {
        return Mathf.FloorToInt(totalSimulatedSeconds / 86400f) + 1;
    }

    public float GetCurrentTimeSeconds()
    {
        return currentTime;
    }

    public float GetTotalSimulatedSeconds()
    {
        return totalSimulatedSeconds;
    }

    public bool IsInTimeRange(int startHour, int endHour)
    {
        int hour = GetHour();

        if (startHour <= endHour)
        {
            return hour >= startHour && hour < endHour;
        }
        else
        {
            return hour >= startHour || hour < endHour;
        }
    }

    public void ResetTimeToMidnight()
    {
        currentTime = 0f;
        totalSimulatedSeconds = 0f;
        DeltaSimSeconds = 0f;
    }

    public void SetStartTime(int hour, int minute)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        currentTime = hour * 3600f + minute * 60f;

        // Experiment elapsed time starts from 0
        totalSimulatedSeconds = 0f;
        DeltaSimSeconds = 0f;
    }

    public void SetTimeScale(float newTimeScale)
    {
        timeScale = Mathf.Max(0f, newTimeScale);
    }
}