using UnityEngine;

// Kill-count best score for Free Play. Query freely; ProceduralMatchFighter
// calls TrySetNewHighScore(...) once a free play run ends.
public static class FreePlayHighScore
{
    private const string Key = "FreePlay_HighScore";

    public static int Get()
    {
        return PlayerPrefs.GetInt(Key, 0);
    }

    public static bool TrySetNewHighScore(int score)
    {
        if (score <= Get())
        {
            return false;
        }
        PlayerPrefs.SetInt(Key, score);
        PlayerPrefs.Save();
        return true;
    }
}
