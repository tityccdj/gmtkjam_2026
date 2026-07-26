using UnityEngine;
using UnityEngine.SceneManagement;

public class GameResultHandler : MonoBehaviour
{
    [SerializeField] 
    private UIGameResult uiGameResult;
    
    [SerializeField] 
    private string mainMenuScene = "Title";

    private void Start()
    {
        // ซ่อนหน้าต่าง UI ไว้ก่อนตอนเริ่มเกม
        if (uiGameResult != null)
        {
            uiGameResult.Hide();
        }
    }

    /// <summary>
    /// เรียกใช้ฟังก์ชันนี้เมื่อจบเกม (ชนะ/แพ้)
    /// </summary>
    /// <param name="playerWon">ผู้เล่นชนะหรือไม่</param>
    public void ShowResult(bool playerWon)
    {
        uiGameResult.Setup(new UIGameResult.Param
        {
            // 1. กดเพื่อไป level ต่อไป เมื่อชนะ CPU ได้
            onNextLevel = (playerWon) ? () => LoadNextLevel() : null,
            
            // 3. กดเพื่อเริ่มใหม่เมื่อ gameover (แสดงเฉพาะตอนแพ้)
            onTryAgain = (!playerWon) ? () => RestartLevel() : null,
            
            // 2. กดเพื่อกลับหน้า menu เมื่อ gameover (แสดงเฉพาะตอนแพ้)
            onMainMenu = (!playerWon) ? () => GoToMainMenu() : null
        });

        uiGameResult.Show();
    }

    public void LoadNextLevel()
    {
        if (LevelSelection.AllLevels != null && LevelSelection.CurrentIndex >= 0)
        {
            int nextIndex = LevelSelection.CurrentIndex + 1;
            if (nextIndex < LevelSelection.AllLevels.Length)
            {
                // อัปเดตข้อมูลด่านต่อไปเข้า LevelSelection
                LevelSelection.CurrentIndex = nextIndex;
                LevelSelection.Current = LevelSelection.AllLevels[nextIndex];
                
                
                // โหลดฉากเกมใหม่ด้วย SceneLoader ถ้ามี
                if (SceneLoader.Instance != null)
                    SceneLoader.Instance.LoadScene(SceneManager.GetActiveScene().name);
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    public void RestartLevel()
    {
        // โหลดฉากปัจจุบันใหม่
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(SceneManager.GetActiveScene().name);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        // กลับไปที่หน้า Main Menu
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(mainMenuScene);
        else
            SceneManager.LoadScene(mainMenuScene);
    }
}
