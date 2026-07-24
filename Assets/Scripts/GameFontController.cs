using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies the game's Yazo typeface to every TMP label in every scene.</summary>
public static class GameFontController
{
    private const string SourceFontPath = "fonts/FreeCheeseR";
    private const string CountdownSourceFontPath = "fonts/FreeCheeseR";
    private static TMP_FontAsset gameFont;
    private static TMP_FontAsset countdownFont;
    private static bool initialized;

    public static TMP_FontAsset Font
    {
        get
        {
            EnsureFont();
            return gameFont;
        }
    }

    public static TMP_FontAsset CountdownFont
    {
        get
        {
            EnsureCountdownFont();
            return countdownFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureFont();
        SceneManager.sceneLoaded += (_, _) => ApplyToScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterInitialSceneLoad()
    {
        ApplyToScene();
    }

    public static void ApplyToScene()
    {
        EnsureFont();
        if (gameFont == null)
        {
            return;
        }

        TMP_Text[] labels = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        foreach (TMP_Text label in labels)
        {
            label.font = gameFont;
        }

        UIBattleHud[] battleHuds = Object.FindObjectsByType<UIBattleHud>(FindObjectsInactive.Include);
        foreach (UIBattleHud battleHud in battleHuds)
        {
            battleHud.ApplyCountdownStyle();
        }
    }

    private static void EnsureFont()
    {
        if (gameFont != null)
        {
            return;
        }

        Font sourceFont = Resources.Load<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"Game font not found at Resources/{SourceFontPath}.ttf");
            return;
        }

        gameFont = TMP_FontAsset.CreateFontAsset(sourceFont);
        gameFont.name = "Yazo SDF";
        gameFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
    }

    private static void EnsureCountdownFont()
    {
        if (countdownFont != null)
        {
            return;
        }

        Font sourceFont = Resources.Load<Font>(CountdownSourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"Countdown font not found at Resources/{CountdownSourceFontPath}.ttf");
            return;
        }

        countdownFont = TMP_FontAsset.CreateFontAsset(sourceFont);
        countdownFont.name = "FreeCheese Countdown SDF";
        countdownFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
    }
}
