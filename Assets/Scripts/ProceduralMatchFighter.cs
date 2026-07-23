using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif


public sealed class ProceduralMatchFighter : MonoBehaviour
{
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

    [Header("Level")]
    [SerializeField] private LevelConfig levelConfig;

    [Header("UI")]
    [SerializeField] private UIBattleHud hud;
    [SerializeField] private UIBattleBoard battleBoard;
    [SerializeField] private UIFighterPanel playerPanel;
    [SerializeField] private UIFighterPanel enemyPanel;

    private int rows;
    private int columns;
    private float turnDuration;
    private Sprite circleSprite;
    private OrbView selectedOrb;
    private OrbView mouseHoverOrb;
    private bool inputReady;
    private bool playerTurn = true;
    private bool boardBusy;
    private bool battleEnded;
    private float timeRemaining;
    private float cpuMoveTimer;
    private float nextNavigationTime;
    private int cursorRow;
    private int cursorColumn;
    private int combo;
    private int killScore;
    private int roundNumber = 1;
    private int enemyAttackBonus;

    private bool IsHumanTurn => playerTurn || playerVsPlayer;
    private bool IsFreePlay => !playerVsPlayer && gameMode == BattleGameMode.FreePlay;

    private void Awake()
    {
        int boardSize = GetBoardSizeForDifficulty();
        rows = boardSize;
        columns = boardSize;
        turnDuration = levelConfig.turnDuration;
        timeRemaining = turnDuration;
        board = new OrbView[rows, columns];

        player.Health = levelConfig.healthCap;
        cpu.Health = levelConfig.healthCap;
        cpu.Name = IsFreePlay ? "ENEMY #1" : levelConfig.enemyName;
        if (playerVsPlayer)
        {
            player.Name = "PLAYER 1";
            cpu.Name = "PLAYER 2";
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>("sprites/circle");
        if (sprites.Length > 0)
        {
            circleSprite = sprites[0];
        }

        battleBoard.ConfigureGrid(rows, columns);
        FillInitialBoard();
        PrepareForInput();
    }

    private int GetBoardSizeForDifficulty()
    {
        switch (enemyDifficulty)
        {
            case EnemyDifficulty.Easy:
                return UnityEngine.Random.Range(4, 6);
            case EnemyDifficulty.Hard:
                return UnityEngine.Random.Range(9, 13);
            default:
                return UnityEngine.Random.Range(6, 9);
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
            if (AnyStartPressed())
            {
                inputReady = true;
                BeginTurn(true);
            }
            return;
        }

        timeRemaining -= Time.deltaTime;
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
            }
        }

        if (timeRemaining <= 0f)
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
                    type = (OrbType)UnityEngine.Random.Range(0, OrbColors.Length);
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
        image.sprite = circleSprite;
        image.color = OrbColors[(int)type];
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
        if (!battleEnded && (!inputReady || IsHumanTurn) && !boardBusy)
        {
            MoveFocusTo(0, 0);
        }
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
        if (selectedOrb == null)
        {
            selectedOrb = orb;
            RefreshSelectionFrames();
            hud.SetMessage("Selected - move to an adjacent orb and press Action");
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
        hud.SetMessage(playerVsPlayer
            ? "PRESS P1 ENTER / P2 0 / GAMEPAD A"
            : "PRESS ENTER / GAMEPAD A");
        hud.SetHook(playerVsPlayer
            ? "P1: WASD + ENTER    P2: 1 2 3 5 + 0"
            : IsFreePlay
                ? $"FREE PLAY  |  {enemyDifficulty.ToString().ToUpper()}  |  BLUE = +1s NEXT TURN"
                : $"STORY  |  {enemyDifficulty.ToString().ToUpper()}  |  BLUE = +1s NEXT TURN");
        MoveFocusTo(0, 0);
        UpdateHud();
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
        return SubmitPressed(0) || SubmitPressed(1);
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
            QueueMatches(matches, playerTurn ? player : cpu, combo);
            yield return DestroyMatches(matches);
            CollapseBoard();
            yield return new WaitForSeconds(0.16f);
            RefillBoard();
            yield return new WaitForSeconds(0.20f);
            matches = FindMatches();
        }

        hud.SetMessage(combo > 1 ? $"CHAIN x{combo}!" : "MATCH!");
        boardBusy = false;
        RefreshSelectionFrames();
    }

    private IEnumerator AnimateSwap(OrbView first, OrbView second)
    {
        Color firstStart = first.Image.color;
        Color secondStart = second.Image.color;
        float elapsed = 0f;
        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.12f;
            first.Image.color = Color.Lerp(firstStart, OrbColors[(int)first.Type], t);
            second.Image.color = Color.Lerp(secondStart, OrbColors[(int)second.Type], t);
            yield return null;
        }

        RefreshOrb(first);
        RefreshOrb(second);
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
        foreach (OrbView orb in matches)
        {
            if (orb.Type == OrbType.Blue)
            {
                owner.Pending[(int)OrbType.Blue] += 1;
                owner.StoredTime += Mathf.RoundToInt(levelConfig.timePerBlueOrb);
            }
            else
            {
                owner.Pending[(int)orb.Type] += chain;
            }
        }
        UpdateHud();
    }

    private IEnumerator DestroyMatches(HashSet<OrbView> matches)
    {
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

                orb.Type = (OrbType)UnityEngine.Random.Range(0, OrbColors.Length);
                orb.Rect.localScale = Vector3.one;
                RefreshOrb(orb);
            }
        }
    }

    private IEnumerator EndTurn()
    {
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

        acting.Health = Mathf.Min(levelConfig.healthCap, acting.Health + heal);
        acting.Shield = Mathf.Min(levelConfig.shieldCap, acting.Shield + shieldGain);
        acting.Special += specialGain;

        int specialBursts = acting.Special / levelConfig.specialBurstThreshold;
        if (specialBursts > 0)
        {
            attack += specialBursts * levelConfig.specialBurstAttackBonus;
            acting.Special %= levelConfig.specialBurstThreshold;
        }

        int maxBlockedThisHit = Mathf.FloorToInt(attack * levelConfig.shieldBlockRatio);
        int blocked = Mathf.Min(target.Shield, maxBlockedThisHit);
        target.Shield -= blocked;
        int damage = attack - blocked;
        target.Health = Mathf.Max(0, target.Health - damage);

        Array.Clear(acting.Pending, 0, acting.Pending.Length);
        UpdateHud();

        hud.SetMessage(BuildResolutionMessage(acting, damage, blocked, heal, shieldGain, timeBonus, specialBursts));
        if (damage > 0)
        {
            UIFighterPanel hitPanel = target == player ? playerPanel : enemyPanel;
            yield return hitPanel.Flash(new Color(1f, 0.14f, 0.12f));
        }
        yield return new WaitForSeconds(1f);

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

        boardBusy = false;
        BeginTurn(!playerTurn);
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
                cpu.Health = Mathf.Min(levelConfig.healthCap, cpu.Health + heal);
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
        cpu.Health = levelConfig.healthCap;
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
        Fighter activeFighter = isPlayer ? player : cpu;
        int storedTime = activeFighter.StoredTime;
        activeFighter.StoredTime = 0;
        timeRemaining = turnDuration + storedTime;
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
        hud.SetHook(
            IsFreePlay
                ? $"KILLS {killScore}  |  ENEMY BONUS ATK +{enemyAttackBonus}"
                : isPlayer
                ? "RACE AGAINST TIME. MATCH WISELY. SURVIVE THE COUNTDOWN."
                : "EVERY 10 SECONDS, DESTINY IS DECIDED.");
        UpdateTimer();
        UpdateHud();
    }

    private void EndBattle(Fighter winner)
    {
        battleEnded = true;
        boardBusy = true;
        RefreshSelectionFrames();
        bool playerOneWon = winner == player;
        hud.SetTurn(
            playerVsPlayer
                ? (playerOneWon ? "PLAYER 1 WINS" : "PLAYER 2 WINS")
                : (playerOneWon ? "VICTORY" : "DEFEAT"),
            playerOneWon
                ? new Color(0.22f, 0.72f, 1f)
                : new Color(1f, 0.27f, 0.30f));
        hud.SetTimer("0", Color.white, false);
        hud.SetMessage(
            IsFreePlay
                ? $"SURVIVED {killScore} KILLS"
                : playerVsPlayer
                ? $"{winner.Name} decided destiny."
                : playerOneWon
                    ? "Destiny favors you."
                    : "The CPU decided your fate.");
        hud.SetHook("Press R / GAMEPAD NORTH to restart");
        StartCoroutine(RestartListener());
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
            ShuffleBoard();
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
            ShuffleBoard();
            return BoardMove.Invalid;
        }
        return validMoves[UnityEngine.Random.Range(0, validMoves.Count)];
    }

    private bool IsValidMove(int rowA, int columnA, int rowB, int columnB)
    {
        OrbView first = board[rowA, columnA];
        OrbView second = board[rowB, columnB];
        SwapTypes(first, second);
        bool valid = FindMatches().Count > 0;
        SwapTypes(first, second);
        return valid;
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

    private void ShuffleBoard()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                board[row, column].Type = (OrbType)UnityEngine.Random.Range(0, OrbColors.Length);
                board[row, column].Rect.localScale = Vector3.one;
                RefreshOrb(board[row, column]);
            }
        }
        hud.SetMessage("No moves - board reshuffled!");
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
        playerPanel.SetStats(
            $"HP {player.Health}/{levelConfig.healthCap}   NEXT +{player.StoredTime}s\n" +
            $"SH {player.Shield}   SP {player.Special}/{levelConfig.specialBurstThreshold}" +
            (IsFreePlay ? $"   KILLS {killScore}" : ""));
        enemyPanel.SetStats(
            $"HP {cpu.Health}/{levelConfig.healthCap}   NEXT +{cpu.StoredTime}s\n" +
            $"SH {cpu.Shield}   SP {cpu.Special}/{levelConfig.specialBurstThreshold}" +
            (IsFreePlay
                ? $"   ATK +{enemyAttackBonus}"
                : $"   {enemyDifficulty.ToString().ToUpper()}"));
        playerPanel.SetHealth((float)player.Health / levelConfig.healthCap);
        enemyPanel.SetHealth((float)cpu.Health / levelConfig.healthCap);

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

    private static void RefreshOrb(OrbView orb)
    {
        orb.Image.color = OrbColors[(int)orb.Type];
        orb.Rect.localScale = Vector3.one;
    }
}
