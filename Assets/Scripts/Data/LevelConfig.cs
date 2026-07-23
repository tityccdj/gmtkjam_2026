using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "GMTK/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Board")]
    public int rows = 6;
    public int columns = 6;
    public float turnDuration = 10f;

    [Header("Fighter Balance")]
    public int healthCap = 100;
    public int manaCap = 100;
    public int manaBurstThreshold = 30;
    public int manaBurstAttackBonus = 8;
    public int shieldCap = 30;
    public int specialBurstThreshold = 12;
    public int specialBurstAttackBonus = 18;
    [Range(0f, 1f)] public float shieldBlockRatio = 0.7f;
    public int attackPerOrb = 4;
    public int manaPerOrb = 2;
    public int healPerOrb = 2;
    public int shieldPerOrb = 1;
    public int specialPerOrb = 1;

    [Header("Enemy")]
    public string enemyName = "CPU";

    [Header("CPU Behavior")]
    public float cpuInitialThinkDelay = 0.7f;
    public float cpuThinkInterval = 1.15f;
    public int redWeight = 5;
    public int blueWeight = 1;
    public int greenWeight = 1;
    public int yellowWeight = 1;
    public int purpleWeight = 3;
    public int lowHealthThreshold = 50;
    public int lowHealthGreenWeight = 6;
    public int lowShieldThreshold = 15;
    public int lowShieldYellowWeight = 4;
}
