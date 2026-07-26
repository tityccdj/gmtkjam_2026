using UnityEngine;

public class IconReposition : MonoBehaviour
{
    private void Start()
    {
        // ตรวจสอบว่าเป็นโหมด Story หรือไม่
        bool isStoryMode = true;
        
        // ถ้า GameMode ถูกเซ็ตเป็นโหมดอื่นที่ไม่ใช่ Story ให้ถือว่าไม่ใช่
        if (LevelSelection.GameMode.HasValue && LevelSelection.GameMode.Value != ProceduralMatchFighter.BattleGameMode.Story)
        {
            isStoryMode = false;
        }

        // ถ้าเป็นโหมด PvP ก็ไม่ใช่ Story
        if (LevelSelection.PlayerVsPlayer.HasValue && LevelSelection.PlayerVsPlayer.Value)
        {
            isStoryMode = false;
        }

        if (isStoryMode)
        {
            // ขยับแกน Y ขึ้น 30 หน่วยโดยใช้ Transform ธรรมดา
            transform.localPosition += new Vector3(0f, 15f, 0f);
        }
    }
}
