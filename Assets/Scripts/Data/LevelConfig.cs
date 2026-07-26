using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "GMTK/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Difficulty")]
    // Drives board size and how fast / how many moves the CPU makes per turn.
    public ProceduralMatchFighter.EnemyDifficulty enemyDifficulty =
        ProceduralMatchFighter.EnemyDifficulty.Normal;
    // Story levels are fought against one of the bosses in BossController; each
    // boss brings its own gimmick and health multiplier.
    public bool isBoss = true;
    public BossController.BossId bossID = BossController.BossId.EntryGate;

    [Header("Board")]
    // Length of a turn. Note the boss can override the player's own turn length
    // (Entry Gate pins it to 10s, TikToker shortens every few turns), but the CPU
    // turn always uses this value - so a shorter turn also means fewer CPU moves.
    public float turnDuration = 10f;

    [Header("Fighter Balance")]
    public int healthCap = 100;
    public float timePerBlueOrb = 1f;
    public int shieldCap = 30;
    public int specialBurstThreshold = 12;
    public int specialBurstAttackBonus = 18;
    [Range(0f, 1f)] public float shieldBlockRatio = 0.7f;
    public int attackPerOrb = 4;
    public int healPerOrb = 2;
    public int shieldPerOrb = 1;
    public int specialPerOrb = 1;

    [Header("Enemy")]
    public string enemyName = "CPU";
    // Spawned in place of the character sitting under EnemyPanel in the Game scene.
    // Leave empty to keep whatever the scene already has.
    public GameObject enemyCharacterPrefab;

    [Header("CPU Behavior")]
    public int redWeight = 5;
    public int blueWeight = 1;
    public int greenWeight = 1;
    public int yellowWeight = 1;
    public int purpleWeight = 3;
    public int lowHealthThreshold = 50;
    public int lowHealthGreenWeight = 6;
    public int lowShieldThreshold = 15;
    public int lowShieldYellowWeight = 4;

    [Header("Environment")]
    public Sprite backgroundSprite;
    public Sprite thumbnail;

    [Header("Tutorial")]
    // Shows contextual in-battle hints (controls + first-seen orb explanations)
    // for as long as this level is played. Only intended for the onboarding level.
    public bool isTutorialLevel;
}
