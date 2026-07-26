using UnityEngine;

public class PlayerReposition : MonoBehaviour
{
    private void Start()
    {
        bool isFreeMode = false;
        
        // เช็คว่าเป็นโหมด Free Play หรือไม่
        if (LevelSelection.GameMode.HasValue && LevelSelection.GameMode.Value == ProceduralMatchFighter.BattleGameMode.FreePlay)
        {
            isFreeMode = true;
        }

        // ถ้าเล่นโหมด PvP (ผู้เล่นแข่งกันเอง) ให้ถือว่าไม่ใช่ Free Mode
        if (LevelSelection.PlayerVsPlayer.HasValue && LevelSelection.PlayerVsPlayer.Value)
        {
            isFreeMode = false;
        }

        if (isFreeMode)
        {
            // ขยับแกน Y ขึ้น 15 หน่วยโดยใช้ Transform
            transform.localPosition += new Vector3(0f, 10f, 0f);
        }
    }
}
