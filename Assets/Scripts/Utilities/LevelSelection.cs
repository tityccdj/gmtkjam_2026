// Carries the LevelConfig chosen in the Title scene's Story Level list, the
// characters picked in the Character Selection panel, and any mode overrides,
// across the scene load into Game.
public static class LevelSelection
{
    public static LevelConfig Current;
    public static bool? PlayerVsPlayer;
    public static ProceduralMatchFighter.BattleGameMode? GameMode;

    // The full story level list and the chosen level's index within it,
    // so Procedural can unlock whatever comes next on a win.
    public static LevelConfig[] AllLevels;
    public static int CurrentIndex = -1;

    // Characters chosen in the Title scene. PlayerCharacter is the one the
    // player picked; OpponentCharacter is only set for the modes that pick the
    // other side automatically (local versus / free play) - in story mode it
    // stays null so the opponent comes from LevelConfig.enemyCharacterPrefab.
    public static CharacterConfig PlayerCharacter;
    public static CharacterConfig OpponentCharacter;
}
