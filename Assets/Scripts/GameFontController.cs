using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies the game's Free Cheese typeface to every TMP label in every scene.</summary>
public static class GameFontController
{
    private const string SourceFontPath = "fonts/FreeCheeseR";
    private static TMP_FontAsset gameFont;
    private static bool initialized;

    public static TMP_FontAsset Font
    {
        get
        {
            EnsureFont();
            return gameFont;
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
        gameFont.name = "FreeCheeseR Runtime TMP";
        gameFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
    }
}
