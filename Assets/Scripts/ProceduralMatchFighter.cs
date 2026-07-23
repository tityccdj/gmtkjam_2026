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

/// <summary>
/// Controller for the Procedural scene's match-3 battle. The board, HUD, and fighter
/// panels are scene-authored (UIBattleHud/UIBattleBoard/UIFighterPanel); this script
/// only owns match rules, turn/CPU logic, and input.
/// </summary>
public sealed class ProceduralMatchFighter : MonoBehaviour
{
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
        public int Health = 100;
        public int Mana;
        public int Shield;
        public int Special;
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

    private const int Rows = 6;
    private const int Columns = 6;
    private const float TurnDuration = 10f;
    private const float CellSize = 72f;
    private const float CellGap = 5f;

    private static readonly Color[] OrbColors =
    {
        new Color(1f, 0.22f, 0.25f),
        new Color(0.20f, 0.62f, 1f),
        new Color(0.25f, 0.90f, 0.43f),
        new Color(1f, 0.83f, 0.20f),
        new Color(0.72f, 0.30f, 1f)
    };

    private static readonly string[] ShortNames = { "ATK", "MP", "HP", "SH", "SP" };

    private readonly OrbView[,] board = new OrbView[Rows, Columns];
    private readonly Fighter player = new Fighter("PLAYER");
    private readonly Fighter cpu = new Fighter("CPU");

    [Header("Game Mode")]
    [SerializeField] private bool playerVsPlayer = false;

    [Header("UI")]
    [SerializeField] private UIBattleHud hud;
    [SerializeField] private UIBattleBoard battleBoard;
    [SerializeField] private UIFighterPanel playerPanel;
    [SerializeField] private UIFighterPanel enemyPanel;

    private Sprite circleSprite;
    private OrbView selectedOrb;
    private OrbView mouseHoverOrb;
    private bool inputReady;
    private bool playerTurn = true;
    private bool boardBusy;
    private bool battleEnded;
    private float timeRemaining = TurnDuration;
    private float cpuMoveTimer;
    private float nextNavigationTime;
    private int cursorRow;
    private int cursorColumn;
    private int combo;

    private bool IsHumanTurn => playerTurn || playerVsPlayer;

    private void Awake()
    {
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

        FillInitialBoard();
        PrepareForInput();
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
                cpuMoveTimer = 1.15f;
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
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
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
        timeRemaining = TurnDuration;
        hud.SetTurn("READY?", new Color(1f, 0.88f, 0.35f));
        hud.SetTimer("10", Color.white, false);
        hud.SetMessage(playerVsPlayer
            ? "PRESS P1 ENTER / P2 0 / GAMEPAD A"
            : "PRESS ENTER / GAMEPAD A");
        hud.SetHook(playerVsPlayer
            ? "P1: WASD + ENTER    P2: 1 2 3 5 + 0"
            : "ENABLE CONTROLS TO BEGIN");
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
                    Mathf.Clamp(cursorRow - direction.y, 0, Rows - 1),
                    Mathf.Clamp(cursorColumn + direction.x, 0, Columns - 1));
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

    private static Vector2 ComputeCellPosition(int row, int column)
    {
        float step = CellSize + CellGap;
        return new Vector2(
            (column - (Columns - 1) * 0.5f) * step,
            ((Rows - 1) * 0.5f - row) * step);
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

        for (int row = 0; row < Rows; row++)
        {
            int runStart = 0;
            for (int column = 1; column <= Columns; column++)
            {
                if (column < Columns && board[row, column].Type == board[row, runStart].Type)
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

        for (int column = 0; column < Columns; column++)
        {
            int runStart = 0;
            for (int row = 1; row <= Rows; row++)
            {
                if (row < Rows && board[row, column].Type == board[runStart, column].Type)
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
            owner.Pending[(int)orb.Type] += chain;
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
        for (int column = 0; column < Columns; column++)
        {
            List<OrbType> survivors = new List<OrbType>();
            for (int row = Rows - 1; row >= 0; row--)
            {
                if (board[row, column].Rect.localScale.x > 0.5f)
                {
                    survivors.Add(board[row, column].Type);
                }
            }

            int survivorIndex = 0;
            for (int row = Rows - 1; row >= 0; row--)
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
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
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

        int attack = acting.Pending[(int)OrbType.Red] * 4;
        int manaGain = acting.Pending[(int)OrbType.Blue] * 2;
        int heal = acting.Pending[(int)OrbType.Green] * 2;
        int shieldGain = acting.Pending[(int)OrbType.Yellow];
        int specialGain = acting.Pending[(int)OrbType.Purple];

        acting.Mana = Mathf.Min(100, acting.Mana + manaGain);
        acting.Health = Mathf.Min(100, acting.Health + heal);
        acting.Shield = Mathf.Min(30, acting.Shield + shieldGain);
        acting.Special += specialGain;

        int specialBursts = acting.Special / 12;
        if (specialBursts > 0)
        {
            attack += specialBursts * 18;
            acting.Special %= 12;
        }

        int manaBursts = acting.Mana / 30;
        if (manaBursts > 0)
        {
            attack += manaBursts * 8;
            acting.Mana %= 30;
        }

        int maxBlockedThisHit = Mathf.FloorToInt(attack * 0.7f);
        int blocked = Mathf.Min(target.Shield, maxBlockedThisHit);
        target.Shield -= blocked;
        int damage = attack - blocked;
        target.Health = Mathf.Max(0, target.Health - damage);

        Array.Clear(acting.Pending, 0, acting.Pending.Length);
        UpdateHud();

        hud.SetMessage(BuildResolutionMessage(acting, damage, blocked, heal, shieldGain, manaGain, specialBursts));
        if (damage > 0)
        {
            UIFighterPanel hitPanel = target == player ? playerPanel : enemyPanel;
            yield return hitPanel.Flash(new Color(1f, 0.14f, 0.12f));
        }
        yield return new WaitForSeconds(1f);

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
        int manaGain,
        int specialBursts)
    {
        string summary = $"{acting.Name}: {damage} DAMAGE";
        if (blocked > 0) summary += $"  |  {blocked} BLOCKED";
        if (heal > 0) summary += $"  |  +{heal} HP";
        if (shieldGain > 0) summary += $"  |  +{shieldGain} SHIELD";
        if (manaGain > 0) summary += $"  |  +{manaGain} MANA";
        if (specialBursts > 0) summary += "  |  SPECIAL!";
        return summary;
    }

    private void BeginTurn(bool isPlayer)
    {
        playerTurn = isPlayer;
        timeRemaining = TurnDuration;
        cpuMoveTimer = 0.7f;
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
            isPlayer
                ? (playerVsPlayer ? "PLAYER 1 TURN" : "PLAYER TURN")
                : (playerVsPlayer ? "PLAYER 2 TURN" : "CPU TURN"),
            isPlayer
                ? new Color(0.22f, 0.72f, 1f)
                : new Color(1f, 0.27f, 0.30f));
        hud.SetMessage(
            isPlayer
                ? "Player 1: build your queue before time runs out."
                : playerVsPlayer
                    ? "Player 2: build your queue before time runs out."
                    : "CPU is planning...");
        hud.SetHook(
            isPlayer
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
            playerVsPlayer
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
        int bestScore = int.MinValue;
        List<BoardMove> bestMoves = new List<BoardMove>();

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                if (column + 1 < Columns)
                {
                    ScoreCpuMove(row, column, row, column + 1, ref bestScore, bestMoves);
                }
                if (row + 1 < Rows)
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
            int weight = 1;
            if (orb.Type == OrbType.Red) weight = 5;
            if (orb.Type == OrbType.Green && cpu.Health < 50) weight = 6;
            if (orb.Type == OrbType.Yellow && cpu.Shield < 15) weight = 4;
            if (orb.Type == OrbType.Purple) weight = 3;
            score += weight;
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

    private void ShuffleBoard()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
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
        string text = shownTime > 3f ? shownTime.ToString("0.0") : Mathf.CeilToInt(shownTime).ToString();
        Color color = shownTime <= 3f
            ? OrbColors[(int)OrbType.Red]
            : new Color(1f, 0.94f, 0.72f);
        hud.SetTimer(text, color, shownTime <= 3f);
    }

    private void UpdateHud()
    {
        playerPanel.SetStats($"HP {player.Health}/100   MP {player.Mana}/30\nSH {player.Shield}   SP {player.Special}/12");
        enemyPanel.SetStats($"HP {cpu.Health}/100   MP {cpu.Mana}/30\nSH {cpu.Shield}   SP {cpu.Special}/12");
        playerPanel.SetHealth(player.Health / 100f);
        enemyPanel.SetHealth(cpu.Health / 100f);

        for (int i = 0; i < ShortNames.Length; i++)
        {
            playerPanel.SetPending(i, $"{ShortNames[i]}\n{player.Pending[i]}", player.Pending[i] > 0 ? OrbColors[i] : Color.white);
            enemyPanel.SetPending(i, $"{ShortNames[i]}\n{cpu.Pending[i]}", cpu.Pending[i] > 0 ? OrbColors[i] : Color.white);
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
