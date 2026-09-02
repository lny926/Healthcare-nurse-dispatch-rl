using UnityEngine;
using TMPro;

public class TimeUI : MonoBehaviour
{
    public TMP_Text timeText;

    void Update()
    {
        if (TimeManager.Instance == null || timeText == null) return;

        timeText.text = "Time: " + TimeManager.Instance.GetFormattedTime();
    }
}