using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossController : MonoBehaviour
{
    public enum BossId
    {
        TheTraitor = 1,
        General = 2,
        Freezer = 3,
        TikToker = 4,
        EntryGate = 5
    }

    public readonly struct Param
    {
        public readonly int BossID;
        public readonly int BaseBossHealth;
        public readonly float DefaultTurnDuration;

        public Param(int bossID, int baseBossHealth, float defaultTurnDuration)
        {
            BossID = Mathf.Clamp(bossID, 1, 5);
            BaseBossHealth = Mathf.Max(1, baseBossHealth);
            DefaultTurnDuration = Mathf.Max(1f, defaultTurnDuration);
        }
    }

    public readonly struct TurnResult
    {
        public readonly bool Triggered;
        public readonly bool TriggerBadEnd;
        public readonly int FreezeBlockCount;
        public readonly int FreezeDuration;
        public readonly string Message;

        public TurnResult(
            bool triggered,
            string message,
            bool triggerBadEnd = false,
            int freezeBlockCount = 0,
            int freezeDuration = 0)
        {
            Triggered = triggered;
            TriggerBadEnd = triggerBadEnd;
            FreezeBlockCount = freezeBlockCount;
            FreezeDuration = freezeDuration;
            Message = message;
        }

        public static TurnResult None => new TurnResult(false, string.Empty);
    }

    [Header("Boss 1 - The Traitor")]
    [SerializeField] private int traitorTotalTurns = 12;
    [SerializeField] private float traitorHealthMultiplier = 1.5f;

    [Header("Boss 2 - General")]
    [SerializeField] private int generalTriggerEveryTurns = 2;
    [SerializeField] private float generalHealthMultiplier = 0.75f;
    [SerializeField] private float generalDamageMultiplier = 1.5f;
    [SerializeField] private float generalGreenDropWeight = 2f;
    [SerializeField] private float generalRedDropWeight = 2f;

    [Header("Boss 3 - Freezer")]
    [SerializeField] private int freezerTriggerEveryTurns = 3;
    [SerializeField] private int freezerBlockCount = 2;
    [SerializeField] private int freezerLockDuration = 2;

    [Header("Boss 4 - TikToker")]
    [SerializeField] private int tikTokerTriggerEveryTurns = 3;
    [SerializeField] private float tikTokerTurnTimeLimit = 6f;
    [SerializeField] private float tikTokerBlueDropWeight = 2.5f;

    [Header("Boss 5 - Entry Gate")]
    [SerializeField] private float entryGateTurnTimeLimit = 10f;

    private BossId activeBoss;
    private float defaultTurnDuration;
    private int completedPlayerTurns;
    private bool generalAttackArmed;
    private bool shortTurnPending;
    private bool blueMatchedThisTurn;
    private bool blueDropBoostActive;

    public bool IsActive { get; private set; }
    public bool HasBossSkill => IsActive && activeBoss != BossId.EntryGate;
    public BossId ActiveBoss => activeBoss;
    public int BossHealthCap { get; private set; }
    public int TotalTurnsLeft { get; private set; }

    public string DisplayName
    {
        get
        {
            switch (activeBoss)
            {
                case BossId.TheTraitor: return "THE TRAITOR";
                case BossId.General: return "GENERAL";
                case BossId.Freezer: return "FREEZER";
                case BossId.TikToker: return "TIKTOKER";
                default: return "ENTRY GATE";
            }
        }
    }

    public void Setup(Param param)
    {
        activeBoss = (BossId)param.BossID;
        defaultTurnDuration = param.DefaultTurnDuration;
        completedPlayerTurns = 0;
        generalAttackArmed = false;
        shortTurnPending = false;
        blueMatchedThisTurn = false;
        blueDropBoostActive = false;
        TotalTurnsLeft = Mathf.Max(1, traitorTotalTurns);
        BossHealthCap = Mathf.Max(
            1,
            Mathf.RoundToInt(param.BaseBossHealth * GetHealthMultiplier()));
        IsActive = true;
    }

    public void OnPlayerTurnStarted()
    {
        if (!IsActive)
        {
            return;
        }

        blueMatchedThisTurn = false;
    }

    public float ConsumePlayerTurnDuration()
    {
        if (!IsActive)
        {
            return defaultTurnDuration;
        }

        if (activeBoss == BossId.EntryGate)
        {
            return Mathf.Max(1f, entryGateTurnTimeLimit);
        }

        if (activeBoss == BossId.TikToker && shortTurnPending)
        {
            shortTurnPending = false;
            return Mathf.Max(1f, tikTokerTurnTimeLimit);
        }

        return defaultTurnDuration;
    }

    public void RecordPlayerMatch(int blueBlockCount)
    {
        if (!IsActive || blueBlockCount <= 0)
        {
            return;
        }

        blueMatchedThisTurn = true;
        if (activeBoss == BossId.TheTraitor)
        {
            TotalTurnsLeft++;
        }
    }

    public TurnResult CompletePlayerTurn()
    {
        if (!HasBossSkill)
        {
            return TurnResult.None;
        }

        completedPlayerTurns++;

        switch (activeBoss)
        {
            case BossId.TheTraitor:
                TotalTurnsLeft--;
                if (TotalTurnsLeft <= 0)
                {
                    return new TurnResult(
                        true,
                        "DOOMSDAY CORE DETONATED!",
                        triggerBadEnd: true);
                }
                return new TurnResult(
                    true,
                    $"DOOMSDAY CORE: {TotalTurnsLeft} TURNS LEFT");

            case BossId.General:
                if (completedPlayerTurns % Mathf.Max(1, generalTriggerEveryTurns) == 0)
                {
                    generalAttackArmed = true;
                    return new TurnResult(
                        true,
                        "ARMOR PIERCING ARMED! NEXT BOSS ATTACK IGNORES SHIELD.");
                }
                break;

            case BossId.Freezer:
                if (completedPlayerTurns % Mathf.Max(1, freezerTriggerEveryTurns) == 0)
                {
                    return new TurnResult(
                        true,
                        $"TILE FREEZE! {freezerBlockCount} BLOCKS LOCKED.",
                        freezeBlockCount: Mathf.Max(1, freezerBlockCount),
                        freezeDuration: Mathf.Max(1, freezerLockDuration));
                }
                break;

            case BossId.TikToker:
                blueDropBoostActive = !blueMatchedThisTurn;
                if (completedPlayerTurns % Mathf.Max(1, tikTokerTriggerEveryTurns) == 0)
                {
                    shortTurnPending = true;
                    return new TurnResult(
                        true,
                        $"TIME HACK! NEXT PLAYER TURN = {tikTokerTurnTimeLimit:0}s");
                }
                if (blueDropBoostActive)
                {
                    return new TurnResult(
                        true,
                        "BLUE DROP RATE UP: NO BLUE MATCH LAST TURN.");
                }
                break;
        }

        return TurnResult.None;
    }

    public int ModifyBossAttack(int baseDamage, out bool ignoreShield)
    {
        ignoreShield = false;
        if (!IsActive || activeBoss != BossId.General || !generalAttackArmed)
        {
            return baseDamage;
        }

        generalAttackArmed = false;
        ignoreShield = true;
        return Mathf.Max(0, Mathf.RoundToInt(baseDamage * generalDamageMultiplier));
    }

    public float GetDropWeight(int orbTypeIndex, bool isPlayerTurn = true)
    {
        if (!IsActive)
        {
            return 1f;
        }

        if (activeBoss == BossId.General)
        {
            if (orbTypeIndex == 0)
            {
                return Mathf.Max(1f, generalRedDropWeight);
            }
            if (orbTypeIndex == 2)
            {
                return Mathf.Max(1f, generalGreenDropWeight);
            }
        }

        if (activeBoss == BossId.TikToker &&
            isPlayerTurn &&
            blueDropBoostActive &&
            orbTypeIndex == 1)
        {
            return Mathf.Max(1f, tikTokerBlueDropWeight);
        }

        return 1f;
    }

    public string GetStatusText()
    {
        if (!IsActive)
        {
            return string.Empty;
        }

        switch (activeBoss)
        {
            case BossId.TheTraitor:
                return $"DOOMSDAY {TotalTurnsLeft} TURNS";
            case BossId.General:
                return generalAttackArmed
                    ? "ARMOR PIERCING READY"
                    : $"ARMOR PIERCING / {generalTriggerEveryTurns} TURNS";
            case BossId.Freezer:
                return
                    $"FREEZE {freezerBlockCount} BLOCKS / " +
                    $"{freezerTriggerEveryTurns} TURNS";
            case BossId.TikToker:
                return shortTurnPending
                    ? "TIME HACK READY"
                    : $"TIME HACK / {tikTokerTriggerEveryTurns} TURNS";
            default:
                return "TUTORIAL BOSS";
        }
    }

    private float GetHealthMultiplier()
    {
        switch (activeBoss)
        {
            case BossId.TheTraitor:
                return Mathf.Max(0.1f, traitorHealthMultiplier);
            case BossId.General:
                return Mathf.Max(0.1f, generalHealthMultiplier);
            default:
                return 1f;
        }
    }
}
