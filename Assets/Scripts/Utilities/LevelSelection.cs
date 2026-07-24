// Carries the LevelConfig chosen in the Title scene's Story Level list,
// and any mode overrides, across the scene load into Procedural.
public static class LevelSelection
{
    public static LevelConfig Current;
    public static bool? PlayerVsPlayer;
    public static ProceduralMatchFighter.BattleGameMode? GameMode;

    // The full story level list and the chosen level's index within it,
    // so Procedural can unlock whatever comes next on a win.
    public static LevelConfig[] AllLevels;
    public static int CurrentIndex = -1;
}
