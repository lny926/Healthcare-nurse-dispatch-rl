using UnityEngine;
using TMPro;

public class ExperimentPanelUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject experimentPanel;

    [Header("Input Fields")]
    public TMP_InputField startHourInput;
    public TMP_InputField startMinuteInput;
    public TMP_InputField durationHoursInput;
    public TMP_InputField timeScaleInput;
    public TMP_InputField seedInput;

    [Header("Dropdown")]
    public TMP_Dropdown strategyDropdown;

    private void Start()
    {
        experimentPanel.SetActive(false);
    }

    // 打开面板
    public void OpenPanel()
    {
        experimentPanel.SetActive(true);

        // 暂停游戏
        Time.timeScale = 0f;
    }

    // 关闭面板
    public void ClosePanel()
    {
        experimentPanel.SetActive(false);

        // 恢复
        Time.timeScale = 1f;
    }

    // 开始实验
    public void StartExperiment()
    {
        ExperimentManager exp = ExperimentManager.Instance;

        if (exp == null)
        {
            Debug.LogError("No ExperimentManager found.");
            return;
        }

        // 读取输入
        exp.startHour = int.Parse(startHourInput.text);
        exp.startMinute = int.Parse(startMinuteInput.text);
        exp.experimentDurationHours = float.Parse(durationHoursInput.text);

        exp.experimentTimeScale = float.Parse(timeScaleInput.text);

        exp.randomSeed = int.Parse(seedInput.text);

        // Dropdown
        exp.experimentMode = (DispatchMode)strategyDropdown.value;

        // 关闭面板
        experimentPanel.SetActive(false);

        // 恢复 Unity 时间
        Time.timeScale = 1f;

        // 开始实验
        exp.StartExperiment();
    }
}