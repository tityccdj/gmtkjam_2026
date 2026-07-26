using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif


public sealed class ProceduralMatchFighter : MonoBehaviour
{
    private const string AntiStuckOverlayResource = "ui/AntiStuckOverlay";
    private const int MaxShuffleAttempts = 256;

    public enum BattleGameMode
    {
        Story,
        FreePlay
    }

    public enum EnemyDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    private enum OrbType
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple
    }

    private sealed class Fighter
    {
        public string Name;
        public int Health;
        public int Shield;
        public int Special;
        public int StoredTime;
        public readonly int[] Pending = new int[5];

        public Fighter(string name)
        {
            Name = name;
        }
    }

    private sealed class OrbView
    {
        public OrbType Type;
        public RectTransform Rect;
        public Image Image;
        public Button Button;
        public int Row;
        public int Column;
        public int LockedPlayerTurns;
    }

    private readonly struct BoardMove
    {
        public readonly int RowA;
        public readonly int ColumnA;
        public readonly int RowB;
        public readonly int ColumnB;

        public BoardMove(int rowA, int columnA, int rowB, int columnB)
        {
            RowA = rowA;
            ColumnA = columnA;
            RowB = rowB;
            ColumnB = columnB;
        }

        public bool IsValid => RowA >= 0;
        public static BoardMove Invalid => new BoardMove(-1, -1, -1, -1);
    }

    private static readonly Color[] OrbColors =
    {
        new Color(1f, 0.22f, 0.25f),
        new Color(0.20f, 0.62f, 1f),
        new Color(0.25f, 0.90f, 0.43f),
        new Color(1f, 0.83f, 0.20f),
        new Color(0.72f, 0.30f, 1f)
    };

    private static readonly string[] ShortNames = { "ATK", "TIME", "HP", "SH", "SP" };

    private OrbView[,] board;
    private readonly Fighter player = new Fighter("PLAYER");
    private readonly Fighter cpu = new Fighter("CPU");

    [Header("Game Mode")]
    [SerializeField] private BattleGameMode gameMode = BattleGameMode.Story;
    [SerializeField] private bool playerVsPlayer = false;
    [SerializeField] private EnemyDifficulty enemyDifficulty = EnemyDifficulty.Normal;

    [Header("Boss")]
    [SerializeField] private bool isBoss;
    [Range(1, 5)]
    [SerializeField] private int bossID = 5;
    [SerializeField] private BossController bossController;

    [Header("Level")]
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private SpriteRenderer background;

    [Header("UI")]
    [SerializeField] private UIBattleHud hud;
    [SerializeField] private UIBattleBoard battleBoard;
    [SerializeField] private UIFighterPanel playerPanel;
    [SerializeField] private UIFighterPanel enemyPanel;
    [SerializeField] private UIRoundTextPanel roundTextPanel;
    [SerializeField] private UIBattleResultSlider battleResultSlider;
    [SerializeField] private GameResultHandler gameResultHandler;

    [Header("Characters")]
    // The character standing on each side. Both are swapped out for the picked
    // character prefabs in ApplyCharacters and drive the attack animations.
    [FormerlySerializedAs("playerCharacter")]
    [SerializeField] private CharacterAnim leftCharacter;
    [FormerlySerializedAs("enemyCharacter")]
    [SerializeField] private CharacterAnim rightCharacter;
    // Parents the picked characters are spawned under. Each falls back to the
    // parent of the character already wired above.
    [SerializeField] private Transform playerCharacterAnchor;
    [SerializeField] private Transform enemyCharacterAnchor;

    private int rows;
    private int columns;
    private float turnDuration;
    private Sprite[] orbSprites;
    private OrbView selectedOrb;
    private OrbView mouseHoverOrb;
    private bool inputReady;
    private bool playerTurn = true;
    private bool boardBusy;
    private bool battleEnded;
    private float timeRemaining;
    private int lastBeepTime = -1;
    private float blinkTimer = 0f;
    private bool hpBlinkState = false;
    private float cpuMoveTimer;
    private float nextNavigationTime;
    private int cursorRow;
    private int cursorColumn;
    private int combo;
    private int killScore;
    private int roundNumber = 1;
    private int enemyAttackBonus;
    private int bossHealthCap;
    private bool reshuffling;
    private GameObject antiStuckOverlay;
    private TMP_Text antiStuckText;
    private CanvasGroup antiStuckCanvasGroup;
    private AudioClip shuffleSound;

    private bool IsHumanTurn => playerTurn || playerVsPlayer;
    private bool IsFreePlay => !playerVsPlayer && gameMode == BattleGameMode.FreePlay;

    private void Awake()
    {
        if (LevelSelection.Current != null)
        {
            levelConfig = LevelSelection.Current;
            background.sprite = levelConfig.backgroundSprite;
            // Story levels carry their own difficulty curve. Free play keeps the
            // difficulty and boss settings authored on this component instead.
            enemyDifficulty = levelConfig.enemyDifficulty;
            isBoss = levelConfig.isBoss;
            bossID = (int)levelConfig.bossID;
        }
        if (LevelSelection.PlayerVsPlayer.HasValue)
        {
            playerVsPlayer = LevelSelection.PlayerVsPlayer.Value;
        }
        if (LevelSelection.GameMode.HasValue)
        {
            gameMode = LevelSelection.GameMode.Value;
        }

        ApplyCharacters();

        int boardSize = GetBoardSizeForDifficulty();
        rows = boardSize;
        columns = boardSize;
        turnDuration = levelConfig.turnDuration;
        timeRemaining = turnDuration;
        board = new OrbView[rows, columns];

        player.Health = levelConfig.healthCap;
        SetupBossController();
        cpu.Health = bossHealthCap;
        cpu.Name = IsFreePlay
            ? "ENEMY #1"
            : bossController != null && bossController.IsActive
                ? bossController.DisplayName
                : levelConfig.enemyName;
        if (playerVsPlayer)
        {
            player.Name = "PLAYER 1";
            cpu.Name = "PLAYER 2";
        }

        orbSprites = new Sprite[5];
        orbSprites[(int)OrbType.Red] = Resources.Load<Sprite>("Orbs/atk_orb");
        orbSprites[(int)OrbType.Blue] = Resources.Load<Sprite>("Orbs/time_orb");
        orbSprites[(int)OrbType.Green] = Resources.Load<Sprite>("Orbs/heal_orb");
        orbSprites[(int)OrbType.Yellow] = Resources.Load<Sprite>("Orbs/shield_orb");
        orbSprites[(int)OrbType.Purple] = Resources.Load<Sprite>("Orbs/special_orb");

        ResolveCharacterAnimations();
        CreateAntiStuckOverlay();
        battleBoard.ConfigureGrid(rows, columns);
        FillInitialBoard();
        battleBoard.gameObject.SetActive(false);
        PrepareForInput();
    }

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            // ปรับตัวเลข 0.5f ลงได้อีกถ้ายังดังไป (เช่น 0.3f, 0.2f)
            AudioManager.Instance.PlayMusic("BGM_gameplay", volumeMultiplier: 0.5f, loop: true, fadeIn: true);
        }
    }

    // Swaps the characters placed in the scene for the ones that were picked.
    // The player side comes from the Title character selection; the enemy side
    // is either the auto-picked opponent (versus / free play) or whatever the
    // story level asks for.
    private void ApplyCharacters()
    {
        leftCharacter = SpawnCharacter(
            LevelSelection.PlayerCharacter != null ? LevelSelection.PlayerCharacter.characterPrefab : null,
            playerCharacterAnchor,
            leftCharacter);

        GameObject enemyPrefab = LevelSelection.OpponentCharacter != null
            ? LevelSelection.OpponentCharacter.characterPrefab
            : null;
        if (enemyPrefab == null && levelConfig != null)
        {
            enemyPrefab = levelConfig.enemyCharacterPrefab;
        }
        rightCharacter = SpawnCharacter(enemyPrefab, enemyCharacterAnchor, rightCharacter);
    }

    // The character already placed in the scene doubles as the placement anchor,
    // so character prefabs don't have to know anything about the arena layout -
    // author them facing right like the player side and the mirroring comes from
    // the placeholder. Returns the character the battle should animate; binding it
    // here (instead of searching the scene afterwards) matters because Destroy is
    // deferred to the end of the frame, so a search would still find the
    // placeholder that is about to disappear.
    private CharacterAnim SpawnCharacter(
        GameObject prefab,
        Transform anchorOverride,
        CharacterAnim placeholderCharacter)
    {
        if (prefab == null)
        {
            return placeholderCharacter;
        }

        Transform placeholder = placeholderCharacter != null ? placeholderCharacter.transform : null;
        Transform anchor = anchorOverride != null
            ? anchorOverride
            : placeholder != null ? placeholder.parent : null;

        if (anchor == null)
        {
            Debug.LogWarning(
                $"No anchor to spawn character prefab '{prefab.name}' under. Assign the character " +
                "anchors or the side characters on ProceduralMatchFighter.");
            return placeholderCharacter;
        }

        Vector3 localPosition = placeholder != null ? placeholder.localPosition : Vector3.zero;
        Quaternion localRotation = placeholder != null ? placeholder.localRotation : Quaternion.identity;
        Vector3 localScale = placeholder != null ? placeholder.localScale : Vector3.one;

        if (placeholder != null)
        {
            Destroy(placeholder.gameObject);
        }

        GameObject spawned = Instantiate(prefab, anchor);
        spawned.name = prefab.name;
        spawned.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        spawned.transform.localScale = Vector3.Scale(prefab.transform.localScale, localScale);

        CharacterAnim spawnedCharacter = spawned.GetComponentInChildren<CharacterAnim>();
        if (spawnedCharacter == null)
        {
            Animator animator = spawned.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                spawnedCharacter = GetOrAddCharacterAnim(animator);
            }
            else
            {
                // Easy to hit by pointing a config straight at an imported .psd,
                // which has the sprite hierarchy but no Animator.
                Debug.LogWarning(
                    $"Character prefab '{prefab.name}' has no Animator, so its attack animation " +
                    "will not play. Use a prefab with an Animator + CharacterAnim on it.");
            }
        }
        return spawnedCharacter;
    }

    private void SetupBossController()
    {
        bossHealthCap = levelConfig.healthCap;
        if (!isBoss || playerVsPlayer)
        {
            return;
        }

        if (bossController == null)
        {
            bossController = GetComponent<BossController>();
        }
        if (bossController == null)
        {
            bossController = gameObject.AddComponent<BossController>();
        }

        bossController.Setup(new BossController.Param(
            bossID,
            levelConfig.healthCap,
            levelConfig.turnDuration));
        bossHealthCap = bossController.BossHealthCap;
    }

    private void ResolveCharacterAnimations()
    {
        CharacterAnim[] characters = FindObjectsByType<CharacterAnim>();
        Array.Sort(characters, (left, right) =>
            left.transform.position.x.CompareTo(right.transform.position.x));
        AssignCharactersBySide(characters);

        if (leftCharacter != null && rightCharacter != null)
        {
            return;
        }

        Animator[] animators = FindObjectsByType<Animator>();
        Array.Sort(animators, (left, right) =>
            left.transform.position.x.CompareTo(right.transform.position.x));

        if (leftCharacter == null)
        {
            Animator leftAnimator = FindAnimatorForSide(animators, true);
            if (leftAnimator != null)
            {
                leftCharacter = GetOrAddCharacterAnim(leftAnimator);
            }
        }
        if (rightCharacter == null)
        {
            Animator rightAnimator = FindAnimatorForSide(animators, false);
            if (rightAnimator != null)
            {
                rightCharacter = GetOrAddCharacterAnim(rightAnimator);
            }
        }
    }

    private void AssignCharactersBySide(CharacterAnim[] characters)
    {
        if (characters.Length == 0)
        {
            return;
        }

        if (characters.Length == 1)
        {
            if (characters[0].transform.position.x <= 0f)
            {
                leftCharacter ??= characters[0];
            }
            else
            {
                rightCharacter ??= characters[0];
            }
            return;
        }

        leftCharacter ??= characters[0];
        rightCharacter ??= characters[characters.Length - 1];
    }

    private Animator FindAnimatorForSide(Animator[] animators, bool findLeft)
    {
        int start = findLeft ? 0 : animators.Length - 1;
        int end = findLeft ? animators.Length : -1;
        int step = findLeft ? 1 : -1;

        for (int index = start; index != end; index += step)
        {
            Animator animator = animators[index];
            CharacterAnim character = animator.GetComponent<CharacterAnim>();
            if (character == leftCharacter || character == rightCharacter)
            {
                continue;
            }

            if (animators.Length == 1)
            {
                bool isOnLeft = animator.transform.position.x <= 0f;
                if (isOnLeft != findLeft)
                {
                    continue;
                }
            }

            return animator;
        }

        return null;
    }

    private static CharacterAnim GetOrAddCharacterAnim(Animator targetAnimator)
    {
        CharacterAnim character = targetAnimator.GetComponent<CharacterAnim>();
        return character != null ? character : targetAnimator.gameObject.AddComponent<CharacterAnim>();
    }

    private int GetBoardSizeForDifficulty()
    {
        switch (enemyDifficulty)
        {
            case EnemyDifficulty.Easy:
                return 5;
            case EnemyDifficulty.Hard:
                return 8;
            default:
                return 6;
        }
    }

    private float GetCpuThinkInterval()
    {
        switch (enemyDifficulty)
        {
            case EnemyDifficulty.Easy: return 1.85f;
            case EnemyDifficulty.Hard: return 0.55f;
            default: return 1.15f;
        }
    }

    private float GetCpuInitialDelay()
    {
        switch (enemyDifficulty)
        {
            case EnemyDifficulty.Easy: return 1.1f;
            case EnemyDifficulty.Hard: return 0.35f;
            default: return 0.7f;
        }
    }

    private void Update()
    {
        if (battleEnded)
        {
            return;
        }

        if (!inputReady)
        {
            if (!boardBusy && AnyStartPressed())
            {
                inputReady = true;
                battleBoard.gameObject.SetActive(true);
                BeginTurn(true);
            }
            return;
        }

        timeRemaining -= Time.deltaTime;
        
        blinkTimer += Time.deltaTime;
        if (blinkTimer >= 0.3f)
        {
            blinkTimer = 0f;
            hpBlinkState = !hpBlinkState;
            if ((player != null && levelConfig != null && player.Health < levelConfig.healthCap * 0.5f) ||
                (cpu != null && cpu.Health < bossHealthCap * 0.5f))
            {
                UpdateHud();
            }
        }

        if (timeRemaining > 0f && timeRemaining <= 3f)
        {
            int currentSecond = Mathf.CeilToInt(timeRemaining);
            if (currentSecond != lastBeepTime && currentSecond > 0)
            {
                lastBeepTime = currentSecond;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFXOneShot("SFX_beep");
                }
            }
        }
        UpdateTimer();

        if (boardBusy)
        {
            return;
        }

        if (IsHumanTurn)
        {
            HandleHumanNavigation(playerTurn ? 0 : 1);
        }
        else
        {
            cpuMoveTimer -= Time.deltaTime;
            if (cpuMoveTimer <= 0f)
            {
                cpuMoveTimer = GetCpuThinkInterval();
                BoardMove move = FindBestCpuMove();
                if (move.IsValid)
                {
                    StartCoroutine(TrySwap(
                        board[move.RowA, move.ColumnA],
                        board[move.RowB, move.ColumnB],
                        false));
                }
                else if (!HasAvailableMove())
                {
                    StartCoroutine(AutoReshuffleBoard());
                }
            }
        }

        if (!boardBusy && timeRemaining <= 0f)
        {
            StartCoroutine(EndTurn());
        }
    }

    private void FillInitialBoard()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                OrbType type;
                do
                {
                    type = GetRandomOrbType();
                }
                while (WouldCreateStartingMatch(row, column, type));

                board[row, column] = CreateOrb(row, column, type);
            }
        }
    }

    private OrbView CreateOrb(int row, int column, OrbType type)
    {
        RectTransform rect = battleBoard.SpawnCell();
        GameObject orbObject = rect.gameObject;
        orbObject.name = $"Orb {row},{column}";

        Image image = orbObject.GetComponent<Image>();
        image.sprite = orbSprites[(int)type];
        image.color = Color.white;
        image.preserveAspect = true;

        Button button = orbObject.GetComponent<Button>();

        OrbView orb = new OrbView
        {
            Type = type,
            Rect = rect,
            Image = image,
            Button = button,
            Row = row,
            Column = column
        };
        button.onClick.AddListener(() => OnOrbPointerClicked(orb));

        EventTrigger trigger = orbObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = orbObject.AddComponent<EventTrigger>();
        }
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => OnOrbPointerEntered(orb));
        trigger.triggers.Add(enter);
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => OnOrbPointerExited(orb));
        trigger.triggers.Add(exit);
        return orb;
    }

    private void OnOrbPointerEntered(OrbView orb)
    {
        if (battleEnded || (inputReady && !IsHumanTurn))
        {
            return;
        }
        mouseHoverOrb = orb;
        MoveFocusTo(orb.Row, orb.Column);
    }

    private void OnOrbPointerExited(OrbView orb)
    {
        if (mouseHoverOrb != orb)
        {
            return;
        }
        mouseHoverOrb = null;
    }

    private void OnOrbPointerClicked(OrbView orb)
    {
        if (!inputReady)
        {
            hud.SetMessage("Press ENTER or GAMEPAD SOUTH to enable controls");
            return;
        }
        MoveFocusTo(orb.Row, orb.Column);
        SubmitFocusedOrb();
    }

    private void SubmitFocusedOrb()
    {
        if (!IsHumanTurn || boardBusy || battleEnded)
        {
            return;
        }

        OrbView orb = board[cursorRow, cursorColumn];
        if (orb.LockedPlayerTurns > 0)
        {
            selectedOrb = null;
            RefreshSelectionFrames();
            hud.SetMessage(
                $"BLOCK FROZEN FOR {orb.LockedPlayerTurns} TURN(S)! " +
                "MATCH IT WITH A CASCADE TO UNLOCK.");
            return;
        }

        if (selectedOrb == null)
        {
            selectedOrb = orb;
            RefreshSelectionFrames();
            hud.SetMessage("");
            return;
        }

        if (selectedOrb == orb)
        {
            selectedOrb = null;
            RefreshSelectionFrames();
            hud.SetMessage("");
            return;
        }

        if (AreAdjacent(selectedOrb, orb))
        {
            OrbView first = selectedOrb;
            selectedOrb = null;
            battleBoard.HideSelection();
            StartCoroutine(TrySwap(first, orb, true));
        }
        else
        {
            selectedOrb = orb;
            RefreshSelectionFrames();
            hud.SetMessage("Selection moved - choose an adjacent orb");
        }
    }

    private void PrepareForInput()
    {
        inputReady = false;
        boardBusy = false;
        timeRemaining = turnDuration;
        hud.SetTurn("READY?", new Color(1f, 0.88f, 0.35f));
        hud.SetTimer(Mathf.CeilToInt(turnDuration).ToString(), Color.white, false);
        hud.SetMessage("PRESS ENTER TO START");
        hud.SetHook(playerVsPlayer
            ? "P1: WASD + ENTER    P2: 1 2 3 5 + 0"
            : IsFreePlay
                ? $"FREE PLAY  |  {enemyDifficulty.ToString().ToUpper()}  |  BLUE = +1s NEXT TURN"
                : $"STORY  |  {enemyDifficulty.ToString().ToUpper()}  |  BLUE = +1s NEXT TURN");
        MoveFocusTo(0, 0);
        UpdateHud();
        if (!HasAvailableMove())
        {
            StartCoroutine(AutoReshuffleBoard());
        }
    }

    private void HandleHumanNavigation(int humanIndex)
    {
        if (mouseHoverOrb == null)
        {
            Vector2Int direction = ReadNavigationDirection(humanIndex);
            if (direction != Vector2Int.zero && Time.unscaledTime >= nextNavigationTime)
            {
                nextNavigationTime = Time.unscaledTime + 0.16f;
                MoveFocusTo(
                    Mathf.Clamp(cursorRow - direction.y, 0, rows - 1),
                    Mathf.Clamp(cursorColumn + direction.x, 0, columns - 1));
            }
        }

        if (SubmitPressed(humanIndex))
        {
            SubmitFocusedOrb();
        }
        else if (CancelPressed(humanIndex) && selectedOrb != null)
        {
            selectedOrb = null;
            RefreshSelectionFrames();
            hud.SetMessage("Selection cancelled");
        }
    }

    private void MoveFocusTo(int row, int column)
    {
        cursorRow = row;
        cursorColumn = column;
        RefreshSelectionFrames();
    }

    private void RefreshSelectionFrames()
    {
        bool canShow = !battleEnded &&
                       !boardBusy &&
                       (!inputReady || IsHumanTurn);
        if (!canShow)
        {
            battleBoard.HideSelection();
            return;
        }

        Color color = selectedOrb != null ? new Color(1f, 0.82f, 0.18f) : Color.white;
        battleBoard.ShowSelectionAt(ComputeCellPosition(cursorRow, cursorColumn), color);
    }

    private Vector2 ComputeCellPosition(int row, int column)
    {
        float step = battleBoard.CellStep;
        return new Vector2(
            (column - (columns - 1) * 0.5f) * step,
            ((rows - 1) * 0.5f - row) * step);
    }

    private static bool AnyStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && 
               (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private static bool SubmitPressed(int humanIndex)
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = false;
        if (Keyboard.current != null)
        {
            keyboard = humanIndex == 0
                ? Keyboard.current.enterKey.wasPressedThisFrame ||
                  Keyboard.current.numpadEnterKey.wasPressedThisFrame
                : Keyboard.current.digit0Key.wasPressedThisFrame ||
                  Keyboard.current.numpad0Key.wasPressedThisFrame;
        }
        Gamepad pad = GetGamepad(humanIndex);
        bool gamepad = pad != null && pad.buttonSouth.wasPressedThisFrame;
        return keyboard || gamepad;
#else
        if (humanIndex == 0)
        {
            return Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.KeypadEnter) ||
                   Input.GetKeyDown(KeyCode.Joystick1Button0);
        }
        return Input.GetKeyDown(KeyCode.Alpha0) ||
               Input.GetKeyDown(KeyCode.Keypad0) ||
               Input.GetKeyDown(KeyCode.Joystick2Button0);
#endif
    }

    private static bool CancelPressed(int humanIndex)
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = humanIndex == 0 &&
                        Keyboard.current != null &&
                        Keyboard.current.escapeKey.wasPressedThisFrame;
        Gamepad pad = GetGamepad(humanIndex);
        bool gamepad = pad != null && pad.buttonEast.wasPressedThisFrame;
        return keyboard || gamepad;
#else
        if (humanIndex == 0)
        {
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Joystick1Button1);
        }
        return Input.GetKeyDown(KeyCode.Joystick2Button1);
#endif
    }

    private static Vector2Int ReadNavigationDirection(int humanIndex)
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (humanIndex == 0)
            {
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
            }
            else
            {
                if (Keyboard.current.digit1Key.isPressed || Keyboard.current.numpad1Key.isPressed) input.x -= 1f;
                if (Keyboard.current.digit3Key.isPressed || Keyboard.current.numpad3Key.isPressed) input.x += 1f;
                if (Keyboard.current.digit2Key.isPressed || Keyboard.current.numpad2Key.isPressed) input.y -= 1f;
                if (Keyboard.current.digit5Key.isPressed || Keyboard.current.numpad5Key.isPressed) input.y += 1f;
            }
        }
        Gamepad pad = GetGamepad(humanIndex);
        if (pad != null)
        {
            input += pad.dpad.ReadValue();
            Vector2 stick = pad.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > 0.55f) input.x += Mathf.Sign(stick.x);
            if (Mathf.Abs(stick.y) > 0.55f) input.y += Mathf.Sign(stick.y);
        }
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) return new Vector2Int((int)Mathf.Sign(input.x), 0);
        if (Mathf.Abs(input.y) > 0.1f) return new Vector2Int(0, (int)Mathf.Sign(input.y));
        return Vector2Int.zero;
#else
        float x = 0f;
        float y = 0f;
        if (humanIndex == 0)
        {
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1)) x -= 1f;
            if (Input.GetKey(KeyCode.Alpha3) || Input.GetKey(KeyCode.Keypad3)) x += 1f;
            if (Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2)) y -= 1f;
            if (Input.GetKey(KeyCode.Alpha5) || Input.GetKey(KeyCode.Keypad5)) y += 1f;
        }
        if (Mathf.Abs(x) > Mathf.Abs(y)) return new Vector2Int((int)Mathf.Sign(x), 0);
        if (Mathf.Abs(y) > 0.1f) return new Vector2Int(0, (int)Mathf.Sign(y));
        return Vector2Int.zero;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static Gamepad GetGamepad(int humanIndex)
    {
        return Gamepad.all.Count > humanIndex ? Gamepad.all[humanIndex] : null;
    }
#endif

    private IEnumerator TrySwap(OrbView first, OrbView second, bool showInvalidMessage)
    {
        boardBusy = true;
        RefreshSelectionFrames();
        SwapTypes(first, second);
        
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXWithPitchVariation("SFX_slide", 0.15f);
        }

        yield return AnimateSwap(first, second);

        HashSet<OrbView> matches = FindMatches();
        if (matches.Count == 0)
        {
            SwapTypes(first, second);
            yield return AnimateInvalidSwap(first, second);
            if (showInvalidMessage)
            {
                hud.SetMessage("No match - choose another move");
            }
            boardBusy = false;
            RefreshSelectionFrames();
            yield break;
        }

        combo = 0;
        while (matches.Count > 0)
        {
            combo++;
            if (AudioManager.Instance != null)
            {
                int comboLevel = Mathf.Clamp(combo, 1, 5);
                AudioManager.Instance.PlaySFXOneShot($"HUD_combo_{comboLevel}");
            }
            QueueMatches(matches, playerTurn ? player : cpu, combo);
            yield return DestroyMatches(matches);
            CollapseBoard();
            yield return new WaitForSeconds(0.16f);
            RefillBoard();
            yield return new WaitForSeconds(0.20f);
            matches = FindMatches();
        }

        bool requiredReshuffle = !HasAvailableMove();
        if (requiredReshuffle)
        {
            yield return AutoReshuffleBoard();
        }
        else
        {
            hud.SetMessage(combo > 1 ? $"CHAIN x{combo}!" : "MATCH!");
        }
        boardBusy = false;
        RefreshSelectionFrames();
    }

    private IEnumerator AnimateSwap(OrbView first, OrbView second)
    {
        float elapsed = 0f;
        while (elapsed < 0.06f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.06f;
            first.Rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            second.Rect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            yield return null;
        }

        RefreshOrb(first);
        RefreshOrb(second);

        elapsed = 0f;
        while (elapsed < 0.06f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.06f;
            first.Rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            second.Rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }

        first.Rect.localScale = Vector3.one;
        second.Rect.localScale = Vector3.one;
    }

    private IEnumerator AnimateInvalidSwap(OrbView first, OrbView second)
    {
        RefreshOrb(first);
        RefreshOrb(second);
        Vector2 firstPosition = first.Rect.anchoredPosition;
        Vector2 secondPosition = second.Rect.anchoredPosition;
        for (int i = 0; i < 4; i++)
        {
            float offset = i % 2 == 0 ? 5f : -5f;
            first.Rect.anchoredPosition = firstPosition + Vector2.right * offset;
            second.Rect.anchoredPosition = secondPosition + Vector2.left * offset;
            yield return new WaitForSeconds(0.035f);
        }
        first.Rect.anchoredPosition = firstPosition;
        second.Rect.anchoredPosition = secondPosition;
    }

    private HashSet<OrbView> FindMatches()
    {
        HashSet<OrbView> found = new HashSet<OrbView>();

        for (int row = 0; row < rows; row++)
        {
            int runStart = 0;
            for (int column = 1; column <= columns; column++)
            {
                if (column < columns && board[row, column].Type == board[row, runStart].Type)
                {
                    continue;
                }

                if (column - runStart >= 3)
                {
                    for (int x = runStart; x < column; x++)
                    {
                        found.Add(board[row, x]);
                    }
                }
                runStart = column;
            }
        }

        for (int column = 0; column < columns; column++)
        {
            int runStart = 0;
            for (int row = 1; row <= rows; row++)
            {
                if (row < rows && board[row, column].Type == board[runStart, column].Type)
                {
                    continue;
                }

                if (row - runStart >= 3)
                {
                    for (int y = runStart; y < row; y++)
                    {
                        found.Add(board[y, column]);
                    }
                }
                runStart = row;
            }
        }

        return found;
    }

    private void QueueMatches(HashSet<OrbView> matches, Fighter owner, int chain)
    {
        PlayScoreAnimation(owner);

        int blueBlockCount = 0;
        HashSet<OrbType> matchedColors = new HashSet<OrbType>();
        
        foreach (OrbView orb in matches)
        {
            if (orb.Type == OrbType.Blue)
            {
                blueBlockCount++;
                owner.Pending[(int)OrbType.Blue] += 1;
                
                int timeAdded = Mathf.RoundToInt(levelConfig.timePerBlueOrb);
                if (owner == cpu)
                {
                    int maxCpuTime = 2; // Capped at exactly 2 seconds
                    if (owner.StoredTime < maxCpuTime)
                    {
                        owner.StoredTime = Mathf.Min(maxCpuTime, owner.StoredTime + timeAdded);
                    }
                }
                else
                {
                    owner.StoredTime += timeAdded;
                }
            }
            else
            {
                owner.Pending[(int)orb.Type] += 1;
                matchedColors.Add(orb.Type);
            }

            // Cascades may clear frozen cells even though the player cannot swap them.
            orb.LockedPlayerTurns = 0;
        }

        if (chain > 1)
        {
            int flatBonus = chain - 1;
            foreach (OrbType type in matchedColors)
            {
                owner.Pending[(int)type] += flatBonus;
            }
        }

        if (owner == player && bossController != null && bossController.IsActive)
        {
            bossController.RecordPlayerMatch(blueBlockCount);
        }
        UpdateHud();
    }

    private void PlayScoreAnimation(Fighter scoringFighter)
    {
        CharacterAnim scoringCharacter =
            scoringFighter == player ? leftCharacter : rightCharacter;
        scoringCharacter?.PlayAttack();
    }

    private IEnumerator DestroyMatches(HashSet<OrbView> matches)
    {
        if (AudioManager.Instance != null && matches != null && matches.Count > 0)
        {
            HashSet<OrbType> matchedTypes = new HashSet<OrbType>();
            foreach (OrbView orb in matches)
            {
                matchedTypes.Add(orb.Type);
            }

            foreach (OrbType type in matchedTypes)
            {
                string soundName = "";
                switch (type)
                {
                    case OrbType.Red: soundName = "SFX_bomb_red"; break;
                    case OrbType.Blue: soundName = "SFX_bomb_blue"; break;
                    case OrbType.Green: soundName = "SFX_bomb_green"; break;
                    case OrbType.Yellow: soundName = "SFX_bomb_yellow"; break;
                    case OrbType.Purple: soundName = "SFX_bomb_purple"; break;
                }

                if (!string.IsNullOrEmpty(soundName))
                {
                    AudioManager.Instance.PlaySFXWithPitchVariation(soundName, 0.15f);
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Clamp01(1f - elapsed / 0.18f);
            foreach (OrbView orb in matches)
            {
                orb.Rect.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        foreach (OrbView orb in matches)
        {
            orb.Rect.localScale = Vector3.zero;
        }
    }

    private void CollapseBoard()
    {
        for (int column = 0; column < columns; column++)
        {
            List<OrbType> survivors = new List<OrbType>();
            for (int row = rows - 1; row >= 0; row--)
            {
                if (board[row, column].Rect.localScale.x > 0.5f)
                {
                    survivors.Add(board[row, column].Type);
                }
            }

            int survivorIndex = 0;
            for (int row = rows - 1; row >= 0; row--)
            {
                if (survivorIndex < survivors.Count)
                {
                    board[row, column].Type = survivors[survivorIndex++];
                    board[row, column].Rect.localScale = Vector3.one;
                    RefreshOrb(board[row, column]);
                }
                else
                {
                    board[row, column].Rect.localScale = Vector3.zero;
                }
            }
        }
    }

    private void RefillBoard()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                OrbView orb = board[row, column];
                if (orb.Rect.localScale.x > 0.5f)
                {
                    continue;
                }

                orb.Type = GetRandomOrbType();
                orb.Rect.localScale = Vector3.one;
                RefreshOrb(orb);
            }
        }
    }

    private Sprite GetSpecialSprite(Fighter fighter)
    {
        string spriteName = "specialmoveimg/sp_man"; // ค่าเริ่มต้นเป็น man
        
        if (fighter == cpu && isBoss)
        {
            switch (bossID)
            {
                case 1: spriteName = "specialmoveimg/sp_woman"; break;
                case 2: spriteName = "specialmoveimg/sp_general"; break;
                case 3: spriteName = "specialmoveimg/sp_freezer"; break;
                case 4: spriteName = "specialmoveimg/sp_tiktok"; break;
                case 5: spriteName = "specialmoveimg/sp_tutor"; break;
            }
        }
        else
        {
            CharacterConfig config = (fighter == player) ? LevelSelection.PlayerCharacter : LevelSelection.OpponentCharacter;
            if (config != null)
            {
                string nameLower = config.displayName.ToLower();
                
                if (nameLower.Contains("woman"))
                {
                    spriteName = "specialmoveimg/sp_woman"; 
                }
                else if (nameLower.Contains("man"))
                {
                    spriteName = "specialmoveimg/sp_man";
                }
            }
        }
        
        return Resources.Load<Sprite>(spriteName);
    }

    private void PlaySpecialSFX(Fighter fighter)
    {
        if (AudioManager.Instance == null) return;
        
        string soundName = "HUD_special_player";
        
        if (fighter == cpu && isBoss)
        {
            switch (bossID)
            {
                case 1: soundName = "HUD_special_boss_lv1"; break;
                case 2: soundName = "HUD_special_boss_lv2"; break;
                case 3: soundName = "HUD_special_boss_lv3"; break;
                case 4: soundName = "HUD_special_boss_lv4"; break;
                // Entry gate (case 5) falls back to HUD_special_player
            }
        }
        
        AudioManager.Instance.PlaySFXOneShot(soundName);
    }

    private IEnumerator EndTurn()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot("SFX_Timeout");
        }
        boardBusy = true;
        RefreshSelectionFrames();
        timeRemaining = 0f;
        UpdateTimer();

        Fighter acting = playerTurn ? player : cpu;
        Fighter target = playerTurn ? cpu : player;
        hud.SetMessage("DESTINY DECIDED!");
        hud.SetHook("RESOLVING THE QUEUE...");
        yield return new WaitForSeconds(0.45f);

        int attack = acting.Pending[(int)OrbType.Red] * levelConfig.attackPerOrb;
        int timeBonus = Mathf.RoundToInt(
            acting.Pending[(int)OrbType.Blue] * levelConfig.timePerBlueOrb);
        int heal = acting.Pending[(int)OrbType.Green] * levelConfig.healPerOrb;
        int shieldGain = acting.Pending[(int)OrbType.Yellow] * levelConfig.shieldPerOrb;
        int specialGain = acting.Pending[(int)OrbType.Purple] * levelConfig.specialPerOrb;

        int actingHealthCap = acting == cpu ? bossHealthCap : levelConfig.healthCap;
        acting.Health = Mathf.Min(actingHealthCap, acting.Health + heal);
        acting.Shield = Mathf.Min(levelConfig.shieldCap, acting.Shield + shieldGain);
        acting.Special += specialGain;

        int specialBursts = acting.Special / levelConfig.specialBurstThreshold;
        if (specialBursts > 0)
        {
            UIFighterPanel actingPanel = playerTurn ? playerPanel : enemyPanel;
            Sprite specialSprite = GetSpecialSprite(acting);
            PlaySpecialSFX(acting);

            if (acting == player && rightCharacter != null)
            {
                StartCoroutine(DelayedPlayEffect("Player_special", rightCharacter.transform, 1.5f));
            }
            else if (acting == cpu && isBoss && leftCharacter != null)
            {
                string vfxName = "";
                switch (bossID)
                {
                    case 1: vfxName = "Traitor_special"; break;
                    case 2: vfxName = "General_special"; break;
                    case 3: vfxName = "Freezer_special"; break;
                    case 4: vfxName = "Tiktoker_special"; break;
                }
                
                if (!string.IsNullOrEmpty(vfxName))
                {
                    StartCoroutine(DelayedPlayEffect(vfxName, leftCharacter.transform, 1.5f));
                }
            }

            yield return actingPanel.ShowSpecialPanel(specialSprite);

            attack += specialBursts * levelConfig.specialBurstAttackBonus;
            acting.Special %= levelConfig.specialBurstThreshold;
        }

        bool ignoreShield = false;
        if (acting == cpu && bossController != null && bossController.IsActive)
        {
            attack = bossController.ModifyBossAttack(attack, out ignoreShield);
        }

        int maxBlockedThisHit = ignoreShield
            ? 0
            : Mathf.FloorToInt(attack * levelConfig.shieldBlockRatio);
        int blocked = Mathf.Min(target.Shield, maxBlockedThisHit);
        if (!ignoreShield)
        {
            target.Shield -= blocked;
        }
        int damage = attack - blocked;
        target.Health = Mathf.Max(0, target.Health - damage);

        Array.Clear(acting.Pending, 0, acting.Pending.Length);
        UpdateHud();

        hud.SetMessage(BuildResolutionMessage(acting, damage, blocked, heal, shieldGain, timeBonus, specialBursts));
        
        if (playerTurn && leftCharacter != null) leftCharacter.PlayAttack();
        if (!playerTurn && rightCharacter != null) rightCharacter.PlayAttack();

        if (damage > 0)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXOneShot("SFX_hit");
            }
            UIFighterPanel hitPanel = target == player ? playerPanel : enemyPanel;
            yield return hitPanel.Flash(new Color(1f, 0.14f, 0.12f));
        }
        yield return new WaitForSeconds(3f);

        if (IsFreePlay)
        {
            if (cpu.Health <= 0)
            {
                RegisterFreePlayKill();
                yield return new WaitForSeconds(0.8f);
            }
            else
            {
                yield return ResolveFreePlayEnemyAction();
            }

            if (player.Health <= 0)
            {
                EndBattle(cpu);
                yield break;
            }

            roundNumber++;
            boardBusy = false;
            BeginTurn(true);
            yield break;
        }

        if (target.Health <= 0)
        {
            EndBattle(acting);
            yield break;
        }

        if (acting == player && bossController != null && bossController.IsActive)
        {
            TickFrozenBlocks();
            BossController.TurnResult bossResult = bossController.CompletePlayerTurn();
            if (bossResult.FreezeBlockCount > 0)
            {
                LockRandomBlocks(
                    bossResult.FreezeBlockCount,
                    bossResult.FreezeDuration);
            }

            if (bossResult.Triggered)
            {
                hud.SetMessage(bossResult.Message);
                UpdateHud();
                yield return new WaitForSeconds(0.8f);
            }

            if (bossResult.TriggerBadEnd)
            {
                EndBattle(cpu, "DOOMSDAY CORE DETONATED! BAD END");
                yield break;
            }
        }

        boardBusy = false;
        BeginTurn(!playerTurn);
    }

    private IEnumerator DelayedPlayEffect(string effectName, Transform target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EffectManager.Instance != null && target != null)
        {
            EffectManager.Instance.PlayEffect(effectName, target.position);
            AudioManager.Instance.PlaySFXOneShot("HUD_combo_5");
        }
    }

    private string BuildResolutionMessage(
        Fighter acting,
        int damage,
        int blocked,
        int heal,
        int shieldGain,
        int timeBonus,
        int specialBursts)
    {
        string summary = $"{acting.Name}: {damage} DAMAGE";
        if (blocked > 0) summary += $"  |  {blocked} BLOCKED";
        if (heal > 0) summary += $"  |  +{heal} HP";
        if (shieldGain > 0) summary += $"  |  +{shieldGain} SHIELD";
        if (timeBonus > 0) summary += $"  |  +{timeBonus}s NEXT TURN";
        if (specialBursts > 0) summary += "  |  SPECIAL!";
        return summary;
    }

    private IEnumerator ResolveFreePlayEnemyAction()
    {
        int action = UnityEngine.Random.Range(0, 4);
        int power;
        switch (enemyDifficulty)
        {
            case EnemyDifficulty.Easy: power = 6; break;
            case EnemyDifficulty.Hard: power = 15; break;
            default: power = 10; break;
        }

        switch (action)
        {
            case 0:
            {
                if (rightCharacter != null) rightCharacter.PlayAttack();
                int attack = power + enemyAttackBonus;
                int blocked = Mathf.Min(
                    player.Shield,
                    Mathf.FloorToInt(attack * levelConfig.shieldBlockRatio));
                player.Shield -= blocked;
                int damage = attack - blocked;
                player.Health = Mathf.Max(0, player.Health - damage);
                hud.SetMessage($"ENEMY ATTACK: {damage} DAMAGE  |  {blocked} BLOCKED");
                UpdateHud();
                if (damage > 0)
                {
                    yield return playerPanel.Flash(new Color(1f, 0.14f, 0.12f));
                }
                break;
            }
            case 1:
            {
                int heal = Mathf.RoundToInt(power * 1.5f);
                cpu.Health = Mathf.Min(bossHealthCap, cpu.Health + heal);
                hud.SetMessage($"ENEMY HEAL: +{heal} HP");
                UpdateHud();
                yield return enemyPanel.Flash(new Color(0.20f, 1f, 0.42f));
                break;
            }
            case 2:
            {
                int buff = enemyDifficulty == EnemyDifficulty.Hard ? 6 :
                    enemyDifficulty == EnemyDifficulty.Easy ? 2 : 4;
                enemyAttackBonus += buff;
                cpu.Shield = Mathf.Min(levelConfig.shieldCap, cpu.Shield + buff);
                hud.SetMessage($"ENEMY POWER UP: +{buff} ATTACK / +{buff} SHIELD");
                UpdateHud();
                yield return enemyPanel.Flash(new Color(1f, 0.83f, 0.20f));
                break;
            }
            default:
            {
                int specialGain = enemyDifficulty == EnemyDifficulty.Hard ? 8 :
                    enemyDifficulty == EnemyDifficulty.Easy ? 4 : 6;
                cpu.Special += specialGain;
                if (cpu.Special >= levelConfig.specialBurstThreshold)
                {
                    Sprite specialSprite = GetSpecialSprite(cpu);
                    PlaySpecialSFX(cpu);
                    yield return enemyPanel.ShowSpecialPanel(specialSprite);
                    if (rightCharacter != null) rightCharacter.PlayAttack();
                    cpu.Special -= levelConfig.specialBurstThreshold;
                    int damage = power + levelConfig.specialBurstAttackBonus + enemyAttackBonus;
                    player.Health = Mathf.Max(0, player.Health - damage);
                    hud.SetMessage($"ENEMY SPECIAL: {damage} DIRECT DAMAGE!");
                    UpdateHud();
                    yield return playerPanel.Flash(new Color(0.72f, 0.30f, 1f));
                }
                else
                {
                    hud.SetMessage($"ENEMY CHARGES SPECIAL: {cpu.Special}/{levelConfig.specialBurstThreshold}");
                    UpdateHud();
                    yield return enemyPanel.Flash(new Color(0.72f, 0.30f, 1f));
                }
                break;
            }
        }

        yield return new WaitForSeconds(0.9f);
    }

    private void RegisterFreePlayKill()
    {
        killScore++;
        enemyAttackBonus += 2;
        cpu.Name = $"ENEMY #{killScore + 1}";
        cpu.Health = bossHealthCap;
        cpu.Shield = 0;
        cpu.Special = 0;
        cpu.StoredTime = 0;
        Array.Clear(cpu.Pending, 0, cpu.Pending.Length);
        hud.SetMessage($"ENEMY DEFEATED!  KILL SCORE: {killScore}");
        UpdateHud();
    }

    private void BeginTurn(bool isPlayer)
    {
        playerTurn = isPlayer;
        if (isPlayer && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot("SFX_startturn");
        }
        Fighter activeFighter = isPlayer ? player : cpu;
        int storedTime = activeFighter.StoredTime;
        activeFighter.StoredTime = 0;
        float activeTurnDuration = turnDuration;
        if (isPlayer && bossController != null && bossController.IsActive)
        {
            bossController.OnPlayerTurnStarted();
            activeTurnDuration = bossController.ConsumePlayerTurnDuration();
        }
        timeRemaining = activeTurnDuration + storedTime;
        lastBeepTime = -1;
        cpuMoveTimer = GetCpuInitialDelay();
        combo = 0;
        selectedOrb = null;
        if (IsHumanTurn)
        {
            if (mouseHoverOrb != null)
            {
                MoveFocusTo(mouseHoverOrb.Row, mouseHoverOrb.Column);
            }
            else
            {
                MoveFocusTo(0, 0);
            }
        }
        else
        {
            battleBoard.HideSelection();
        }
        hud.SetTurn(
            IsFreePlay
                ? $"FREE PLAY  -  ROUND {roundNumber}"
                : isPlayer
                ? (playerVsPlayer ? "PLAYER 1 TURN" : "PLAYER TURN")
                : (playerVsPlayer ? "PLAYER 2 TURN" : "CPU TURN"),
            isPlayer
                ? new Color(0.22f, 0.72f, 1f)
                : new Color(1f, 0.27f, 0.30f));
        hud.SetMessage(
            IsFreePlay
                ? $"Defeat {cpu.Name}. Blue stores +{levelConfig.timePerBlueOrb:0}s for your next turn."
                : isPlayer
                ? "Player 1: build your queue before time runs out."
                : playerVsPlayer
                    ? "Player 2: build your queue before time runs out."
                    : "CPU is planning...");
        string hook =
            IsFreePlay
                ? $"KILLS {killScore}  |  ENEMY BONUS ATK +{enemyAttackBonus}"
                : isPlayer
                ? "RACE AGAINST TIME. MATCH WISELY. SURVIVE THE COUNTDOWN."
                : "EVERY 10 SECONDS, DESTINY IS DECIDED.";
        if (bossController != null && bossController.IsActive)
        {
            hook += $"  |  {bossController.GetStatusText()}";
        }
        hud.SetHook(hook);
        UpdateTimer();
        UpdateHud();
        if (!HasAvailableMove())
        {
            StartCoroutine(AutoReshuffleBoard());
        }
    }

    private void EndBattle(Fighter winner, string overrideMessage = null)
    {
        battleEnded = true;
        boardBusy = true;
        RefreshSelectionFrames();
        bool playerOneWon = winner == player;

        if (playerOneWon && !playerVsPlayer && !IsFreePlay)
        {
            UnlockNextStoryLevel();
        }

        if (roundTextPanel != null)
        {
            roundTextPanel.ShowRoundText(playerOneWon);
        }
        if (battleResultSlider != null)
        {
            battleResultSlider.ShowResult(playerOneWon);
        }
        hud.SetTurn(
            playerVsPlayer
                ? (playerOneWon ? "PLAYER 1 WINS" : "PLAYER 2 WINS")
                : (playerOneWon ? "VICTORY" : "DEFEAT"),
            playerOneWon
                ? new Color(0.22f, 0.72f, 1f)
                : new Color(1f, 0.27f, 0.30f));
        hud.SetTimer("0", Color.white, false);
        hud.SetMessage(
            !string.IsNullOrEmpty(overrideMessage)
                ? overrideMessage
                : IsFreePlay
                ? $"SURVIVED {killScore} KILLS"
                : playerVsPlayer
                ? $"{winner.Name} decided destiny."
                : playerOneWon
                    ? "Destiny favors you."
                    : "The CPU decided your fate.");
        hud.SetHook("Press R / GAMEPAD NORTH to restart");

        if (AudioManager.Instance != null)
        {
            if (playerOneWon)
            {
                AudioManager.Instance.StopMusic(fadeOut: true);
                AudioManager.Instance.PlaySFXOneShot("HUD_win");
            }
            else
            {
                AudioManager.Instance.StopMusic(fadeOut: true);
                AudioManager.Instance.PlaySFXOneShot("HUD_lose");
            }
        }

        if (gameResultHandler != null)
        {
            gameResultHandler.ShowResult(playerOneWon);
        }
        else
        {
            StartCoroutine(RestartListener());
        }
    }

    private static void UnlockNextStoryLevel()
    {
        if (LevelSelection.AllLevels == null || LevelSelection.CurrentIndex < 0)
        {
            return;
        }

        int nextIndex = LevelSelection.CurrentIndex + 1;
        if (nextIndex < LevelSelection.AllLevels.Length)
        {
            LevelSaveState.SetUnlocked(LevelSelection.AllLevels[nextIndex], true);
        }
    }

    private IEnumerator RestartListener()
    {
        while (true)
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardRestart = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            bool gamepadRestart = false;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.buttonNorth.wasPressedThisFrame)
                {
                    gamepadRestart = true;
                    break;
                }
            }
            if (keyboardRestart || gamepadRestart)
#else
            if (Input.GetKeyDown(KeyCode.R))
#endif
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield break;
            }
            yield return null;
        }
    }

    private BoardMove FindBestCpuMove()
    {
        if (enemyDifficulty == EnemyDifficulty.Easy)
        {
            return FindRandomCpuMove();
        }

        int bestScore = int.MinValue;
        List<BoardMove> bestMoves = new List<BoardMove>();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (column + 1 < columns)
                {
                    ScoreCpuMove(row, column, row, column + 1, ref bestScore, bestMoves);
                }
                if (row + 1 < rows)
                {
                    ScoreCpuMove(row, column, row + 1, column, ref bestScore, bestMoves);
                }
            }
        }

        if (bestMoves.Count == 0)
        {
            return BoardMove.Invalid;
        }
        return bestMoves[UnityEngine.Random.Range(0, bestMoves.Count)];
    }

    private BoardMove FindRandomCpuMove()
    {
        List<BoardMove> validMoves = new List<BoardMove>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (column + 1 < columns &&
                    IsValidMove(row, column, row, column + 1))
                {
                    validMoves.Add(new BoardMove(row, column, row, column + 1));
                }
                if (row + 1 < rows &&
                    IsValidMove(row, column, row + 1, column))
                {
                    validMoves.Add(new BoardMove(row, column, row + 1, column));
                }
            }
        }

        if (validMoves.Count == 0)
        {
            return BoardMove.Invalid;
        }
        return validMoves[UnityEngine.Random.Range(0, validMoves.Count)];
    }

    private bool IsValidMove(int rowA, int columnA, int rowB, int columnB)
    {
        OrbView first = board[rowA, columnA];
        OrbView second = board[rowB, columnB];
        if (playerTurn &&
            (first.LockedPlayerTurns > 0 || second.LockedPlayerTurns > 0))
        {
            return false;
        }

        SwapTypes(first, second);
        bool valid = FindMatches().Count > 0;
        SwapTypes(first, second);
        return valid;
    }

    private void TickFrozenBlocks()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                OrbView orb = board[row, column];
                if (orb.LockedPlayerTurns <= 0)
                {
                    continue;
                }

                orb.LockedPlayerTurns--;
                RefreshOrb(orb);
            }
        }
    }

    private void LockRandomBlocks(int count, int duration)
    {
        List<OrbView> candidates = new List<OrbView>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (board[row, column].LockedPlayerTurns <= 0)
                {
                    candidates.Add(board[row, column]);
                }
            }
        }

        int lockCount = Mathf.Min(Mathf.Max(0, count), candidates.Count);
        for (int i = 0; i < lockCount; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            OrbView orb = candidates[index];
            candidates.RemoveAt(index);
            orb.LockedPlayerTurns = Mathf.Max(1, duration);
            RefreshOrb(orb);
        }
    }

    private void ScoreCpuMove(
        int rowA,
        int columnA,
        int rowB,
        int columnB,
        ref int bestScore,
        List<BoardMove> bestMoves)
    {
        OrbView first = board[rowA, columnA];
        OrbView second = board[rowB, columnB];
        SwapTypes(first, second);
        HashSet<OrbView> matches = FindMatches();

        int score = 0;
        foreach (OrbView orb in matches)
        {
            score += WeightForOrb(orb.Type);
        }
        SwapTypes(first, second);

        if (matches.Count == 0)
        {
            return;
        }

        BoardMove move = new BoardMove(rowA, columnA, rowB, columnB);
        if (score > bestScore)
        {
            bestScore = score;
            bestMoves.Clear();
            bestMoves.Add(move);
        }
        else if (score == bestScore)
        {
            bestMoves.Add(move);
        }
    }

    private int WeightForOrb(OrbType type)
    {
        switch (type)
        {
            case OrbType.Red:
                return levelConfig.redWeight;
            case OrbType.Blue:
                return levelConfig.blueWeight;
            case OrbType.Green:
                return cpu.Health < levelConfig.lowHealthThreshold
                    ? levelConfig.lowHealthGreenWeight
                    : levelConfig.greenWeight;
            case OrbType.Yellow:
                return cpu.Shield < levelConfig.lowShieldThreshold
                    ? levelConfig.lowShieldYellowWeight
                    : levelConfig.yellowWeight;
            case OrbType.Purple:
                return levelConfig.purpleWeight;
            default:
                return 1;
        }
    }

    private bool HasAvailableMove()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (column + 1 < columns &&
                    IsValidMove(row, column, row, column + 1))
                {
                    return true;
                }
                if (row + 1 < rows &&
                    IsValidMove(row, column, row + 1, column))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator AutoReshuffleBoard()
    {
        if (reshuffling)
        {
            yield break;
        }

        reshuffling = true;
        boardBusy = true;
        selectedOrb = null;
        battleBoard.HideSelection();
        hud.SetMessage("NO MOVES! RESHUFFLING...");
        ShowAntiStuckOverlay();
        PlayShuffleSound();

        yield return ScaleBoard(Vector3.one, Vector3.one * 0.12f, 0.14f);

        bool shuffled = TryCreatePlayableShuffle();
        if (!shuffled)
        {
            Debug.LogError("Anti-Stuck System could not create a playable board.", this);
        }
        RefreshAllOrbs();

        yield return ScaleBoard(Vector3.one * 0.12f, Vector3.one, 0.24f);
        yield return new WaitForSeconds(0.55f);

        cpuMoveTimer = GetCpuInitialDelay();
        UpdateTimer();
        HideAntiStuckOverlay();
        hud.SetMessage("BOARD READY!");
        reshuffling = false;
        boardBusy = false;
        RefreshSelectionFrames();
    }

    private bool TryCreatePlayableShuffle()
    {
        List<OrbType> types = new List<OrbType>(rows * columns);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                types.Add(board[row, column].Type);
            }
        }

        for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
        {
            ShuffleTypes(types);
            ApplyTypes(types);
            if (FindMatches().Count == 0 && HasAvailableMove())
            {
                return true;
            }
        }

        for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
        {
            GenerateBoardWithoutStartingMatches();
            if (HasAvailableMove())
            {
                return true;
            }
        }

        return false;
    }

    private static void ShuffleTypes(List<OrbType> types)
    {
        for (int index = types.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (types[index], types[swapIndex]) = (types[swapIndex], types[index]);
        }
    }

    private void ApplyTypes(List<OrbType> types)
    {
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                board[row, column].Type = types[index++];
            }
        }
    }

    private void GenerateBoardWithoutStartingMatches()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                OrbType type;
                do
                {
                    type = GetRandomOrbType();
                }
                while (WouldCreateStartingMatch(row, column, type));
                board[row, column].Type = type;
            }
        }
    }

    private OrbType GetRandomOrbType()
    {
        float totalWeight = 0f;
        for (int index = 0; index < OrbColors.Length; index++)
        {
            totalWeight += bossController != null && bossController.IsActive
                ? bossController.GetDropWeight(index, playerTurn)
                : 1f;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        for (int index = 0; index < OrbColors.Length; index++)
        {
            roll -= bossController != null && bossController.IsActive
                ? bossController.GetDropWeight(index, playerTurn)
                : 1f;
            if (roll <= 0f)
            {
                return (OrbType)index;
            }
        }

        return OrbType.Purple;
    }

    private void RefreshAllOrbs()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                board[row, column].Rect.localScale = Vector3.one;
                RefreshOrb(board[row, column]);
            }
        }
    }

    private IEnumerator ScaleBoard(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 scale = Vector3.Lerp(from, to, progress);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    board[row, column].Rect.localScale = scale;
                }
            }
            yield return null;
        }

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                board[row, column].Rect.localScale = to;
            }
        }
    }

    private void CreateAntiStuckOverlay()
    {
        GameObject overlayPrefab = Resources.Load<GameObject>(AntiStuckOverlayResource);
        if (overlayPrefab == null)
        {
            Debug.LogWarning(
                $"Missing Resources/{AntiStuckOverlayResource}.prefab.",
                this);
            return;
        }

        Canvas canvas = hud.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : hud.transform;
        antiStuckOverlay = Instantiate(overlayPrefab, parent, false);
        antiStuckOverlay.name = "AntiStuckOverlay";
        antiStuckOverlay.transform.SetAsLastSibling();

        RectTransform panelRect = antiStuckOverlay.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one * 0.5f;
        panelRect.anchorMax = Vector2.one * 0.5f;
        panelRect.pivot = Vector2.one * 0.5f;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(780f, 150f);

        GameObject textObject = new GameObject(
            "Message",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = antiStuckOverlay.layer;
        textObject.transform.SetParent(antiStuckOverlay.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(28f, 12f);
        textRect.offsetMax = new Vector2(-28f, -12f);

        antiStuckText = textObject.GetComponent<TextMeshProUGUI>();
        antiStuckText.text = "NO MOVES! RESHUFFLING...";
        antiStuckText.alignment = TextAlignmentOptions.Center;
        antiStuckText.fontStyle = FontStyles.Bold;
        antiStuckText.fontSize = 46f;
        antiStuckText.enableAutoSizing = true;
        antiStuckText.fontSizeMin = 24f;
        antiStuckText.fontSizeMax = 52f;
        antiStuckText.color = new Color(1f, 0.88f, 0.35f);
        antiStuckText.raycastTarget = false;
        if (GameFontController.Font != null)
        {
            antiStuckText.font = GameFontController.Font;
        }

        antiStuckCanvasGroup = antiStuckOverlay.AddComponent<CanvasGroup>();
        antiStuckCanvasGroup.blocksRaycasts = false;
        antiStuckCanvasGroup.interactable = false;
        antiStuckOverlay.SetActive(false);
    }

    private void ShowAntiStuckOverlay()
    {
        if (antiStuckOverlay == null)
        {
            return;
        }

        antiStuckOverlay.transform.SetAsLastSibling();
        antiStuckOverlay.transform.localScale = Vector3.one * 0.85f;
        antiStuckCanvasGroup.alpha = 0f;
        antiStuckOverlay.SetActive(true);
        LeanTween.cancel(antiStuckOverlay);
        LeanTween.alphaCanvas(antiStuckCanvasGroup, 1f, 0.12f);
        LeanTween.scale(antiStuckOverlay, Vector3.one, 0.18f)
            .setEaseOutBack();
    }

    private void HideAntiStuckOverlay()
    {
        if (antiStuckOverlay == null)
        {
            return;
        }

        LeanTween.cancel(antiStuckOverlay);
        antiStuckOverlay.SetActive(false);
    }

    private void PlayShuffleSound()
    {
        shuffleSound ??= CreateShuffleSound();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot(shuffleSound, 0.8f);
        }
    }

    private static AudioClip CreateShuffleSound()
    {
        const int sampleRate = 22050;
        const float duration = 0.34f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)sampleRate;
            float progress = time / duration;
            float envelope = Mathf.Pow(1f - progress, 1.8f);
            float sweep = Mathf.Sin(
                2f * Mathf.PI * (160f * time + 920f * time * time));
            float noise = Mathf.PerlinNoise(index * 0.071f, 0.37f) * 2f - 1f;
            float tick = Mathf.Sin(2f * Mathf.PI * 42f * time) > 0.78f ? 1f : 0.2f;
            samples[index] = (sweep * 0.52f + noise * 0.22f) * envelope * tick;
        }

        AudioClip clip = AudioClip.Create(
            "AntiStuckShuffle",
            sampleCount,
            1,
            sampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void UpdateTimer()
    {
        float shownTime = Mathf.Max(0f, timeRemaining);
        string text = Mathf.CeilToInt(shownTime).ToString();
        Color color = shownTime <= 3f
            ? OrbColors[(int)OrbType.Red]
            : new Color(1f, 0.94f, 0.72f);
        hud.SetTimer(text, color, shownTime <= 3f);
    }

    private void UpdateHud()
    {
        playerPanel.SetName(player.Name);
        enemyPanel.SetName(cpu.Name);
        
        string playerHpColor = (player.Health < levelConfig.healthCap * 0.5f && hpBlinkState) ? "<color=red>" : "<color=white>";
        string cpuHpColor = (cpu.Health < bossHealthCap * 0.5f && hpBlinkState) ? "<color=red>" : "<color=white>";

        playerPanel.SetStats(
            $"{playerHpColor}HP {player.Health}/{levelConfig.healthCap}</color>\n" +
            $"<color=yellow>Shield {player.Shield}</color>\n" +
            $"SP {player.Special}/{levelConfig.specialBurstThreshold}\n" +
            $"NEXT +{player.StoredTime}s" +
            (IsFreePlay ? $"\nKILLS {killScore}" : ""));
        enemyPanel.SetStats(
            $"{cpuHpColor}HP {cpu.Health}/{bossHealthCap}</color>\n" +
            $"<color=yellow>Shield {cpu.Shield}</color>\n" +
            $"SP {cpu.Special}/{levelConfig.specialBurstThreshold}\n" +
            $"NEXT +{cpu.StoredTime}s" +
            (IsFreePlay
                ? $"\nATK +{enemyAttackBonus}"
                : $"\n{enemyDifficulty.ToString().ToUpper()}") +
            (bossController != null && bossController.IsActive
                ? $"\n{bossController.GetStatusText()}"
                : ""));
        playerPanel.SetHealth((float)player.Health / levelConfig.healthCap);
        enemyPanel.SetHealth((float)cpu.Health / bossHealthCap);

        for (int i = 0; i < ShortNames.Length; i++)
        {
            string playerValue = i == (int)OrbType.Blue
                ? $"+{player.Pending[i] * levelConfig.timePerBlueOrb:0}s"
                : player.Pending[i].ToString();
            string enemyValue = i == (int)OrbType.Blue
                ? $"+{cpu.Pending[i] * levelConfig.timePerBlueOrb:0}s"
                : cpu.Pending[i].ToString();
            playerPanel.SetPending(i, $"{ShortNames[i]}\n{playerValue}", player.Pending[i] > 0 ? OrbColors[i] : Color.white);
            enemyPanel.SetPending(i, $"{ShortNames[i]}\n{enemyValue}", cpu.Pending[i] > 0 ? OrbColors[i] : Color.white);
        }
    }

    private bool WouldCreateStartingMatch(int row, int column, OrbType type)
    {
        bool horizontal = column >= 2 &&
                          board[row, column - 1] != null &&
                          board[row, column - 2] != null &&
                          board[row, column - 1].Type == type &&
                          board[row, column - 2].Type == type;
        bool vertical = row >= 2 &&
                        board[row - 1, column] != null &&
                        board[row - 2, column] != null &&
                        board[row - 1, column].Type == type &&
                        board[row - 2, column].Type == type;
        return horizontal || vertical;
    }

    private static bool AreAdjacent(OrbView first, OrbView second)
    {
        int distance = Mathf.Abs(first.Row - second.Row) + Mathf.Abs(first.Column - second.Column);
        return distance == 1;
    }

    private static void SwapTypes(OrbView first, OrbView second)
    {
        (first.Type, second.Type) = (second.Type, first.Type);
    }

    private void RefreshOrb(OrbView orb)
    {
        orb.Image.sprite = orbSprites[(int)orb.Type];
        orb.Image.color = orb.LockedPlayerTurns > 0 ? new Color(0.5f, 0.8f, 1f, 1f) : Color.white;
        orb.Rect.localScale = Vector3.one;
    }
}
