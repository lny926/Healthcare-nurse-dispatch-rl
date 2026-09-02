using UnityEngine;
using UnityEngine.SceneManagement;


public class UIManager : MonoBehaviour
{
    private bool isPaused = false;

    public void TogglePause()
    {
        if (isPaused)
        {
            Time.timeScale = 1f;  // 恢复
            isPaused = false;
        }
        else
        {
            Time.timeScale = 0f;  // 暂停
            isPaused = true;
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");

        Application.Quit();

        // 在编辑器里不会退出，所以加这个
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}