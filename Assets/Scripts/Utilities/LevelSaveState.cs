using UnityEngine;

// Lock/unlock persistence for story levels. Query freely; the gameplay team
// calls SetUnlocked(...) once level-completion logic exists.
public static class LevelSaveState
{
    private const string UnlockedKeyPrefix = "Level_Unlocked_";

    public static bool IsUnlocked(LevelConfig level, bool defaultUnlocked = false)
    {
        if (level == null) return false;
        return PlayerPrefs.GetInt(UnlockedKeyPrefix + level.name, defaultUnlocked ? 1 : 0) == 1;
    }

    public static void SetUnlocked(LevelConfig level, bool unlocked)
    {
        if (level == null) return;
        PlayerPrefs.SetInt(UnlockedKeyPrefix + level.name, unlocked ? 1 : 0);
        PlayerPrefs.Save();
    }
}
