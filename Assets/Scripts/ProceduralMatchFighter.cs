using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Self-contained prototype for the Procedural scene.
/// It builds the board and battle HUD at runtime so the arena artwork can keep changing.
/// </summary>
public sealed class ProceduralMatchFighter : MonoBehaviour
{
    [Serializable]
    private sealed class SceneElementMap
    {
        public Transform panelPlayer;
        public Transform panelEnemy;
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
        public readonly string Name;
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

    private static readonly string[] OrbNames =
    {
        "ATTACK", "MANA", "HEAL", "SHIELD", "SPECIAL"
    };

    private readonly OrbView[,] board = new OrbView[Rows, Columns];
    private readonly Fighter player = new Fighter("PLAYER");
    private readonly Fighter cpu = new Fighter("CPU");
    private readonly List<TMP_Text> playerPendingTexts = new List<TMP_Text>();
    private readonly List<TMP_Text> cpuPendingTexts = new List<TMP_Text>();

    [SerializeField] private SceneElementMap elementMap = new SceneElementMap();

    private Sprite circleSprite;
    private Sprite[] selectionSprites;
    private RectTransform boardShell;
    private RectTransform boardRoot;
    private RectTransform focusFrame;
    private RectTransform lockedFrame;
    private TMP_Text timerText;
    private TMP_Text turnText;
    private TMP_Text messageText;
    private TMP_Text hookText;
    private TMP_Text playerStatsText;
    private TMP_Text cpuStatsText;
    private Image playerHealthFill;
    private Image cpuHealthFill;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForProceduralScene()
    {
        if (SceneManager.GetActiveScene().name != "Procedural" ||
            FindAnyObjectByType<ProceduralMatchFighter>() != null)
        {
            return;
        }

        GameObject host = new GameObject(nameof(ProceduralMatchFighter));
        host.AddComponent<ProceduralMatchFighter>();
    }

    private void Awake()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("sprites/circle");
        if (sprites.Length > 0)
        {
            circleSprite = sprites[0];
        }
        selectionSprites = Resources.LoadAll<Sprite>("sprites/selection");
        Array.Sort(selectionSprites, (left, right) => string.CompareOrdinal(left.name, right.name));

        ResolveElementMap();
        EnsureEventSystem();
        BuildInterface();
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
            if (SubmitPressed())
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

        if (playerTurn)
        {
            HandlePlayerNavigation();
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

    private void ResolveElementMap()
    {
        if (elementMap.panelPlayer == null)
        {
            GameObject panel = GameObject.Find("PanelPlayer");
            elementMap.panelPlayer = panel != null ? panel.transform : null;
        }
        if (elementMap.panelEnemy == null)
        {
            GameObject panel = GameObject.Find("Panelenemy");
            elementMap.panelEnemy = panel != null ? panel.transform : null;
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Match Fighter HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1536f, 1024f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        Stretch(root);

        boardShell = CreatePanel(root, "Board Element", Color.clear);
        SetRect(boardShell, new Vector2(0.5f, 0.50f), new Vector2(482f, 482f), Vector2.zero);

        boardRoot = CreatePanel(boardShell, "Orb Grid", new Color(0.01f, 0.025f, 0.055f, 0.68f));
        SetRect(boardRoot, new Vector2(0.5f, 0.5f), new Vector2(482f, 482f), Vector2.zero);

        GridLayoutGroup grid = boardRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CellSize, CellSize);
        grid.spacing = new Vector2(CellGap, CellGap);
        grid.padding = new RectOffset(13, 13, 13, 13);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        CreateHeader(root);
        CreateMappedFighterHud(elementMap.panelPlayer, player, true);
        CreateMappedFighterHud(elementMap.panelEnemy, cpu, false);

        messageText = CreateText(root, "Battle Message", "", 26, TextAnchor.MiddleCenter, Color.white);
        SetRect(messageText.rectTransform, new Vector2(0.5f, 0.115f), new Vector2(700f, 48f), Vector2.zero);

        hookText = CreateText(
            root,
            "Hook",
            "EVERY 10 SECONDS, DESTINY IS DECIDED.",
            18,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.88f, 0.35f));
        hookText.fontStyle = FontStyles.Bold;
        SetRect(hookText.rectTransform, new Vector2(0.5f, 0.065f), new Vector2(760f, 36f), Vector2.zero);

        focusFrame = CreateSelectionFrame(boardShell, "Focus Selection", Color.white);
        lockedFrame = CreateSelectionFrame(boardShell, "Locked Selection", new Color(1f, 0.82f, 0.18f));
        focusFrame.gameObject.SetActive(false);
        lockedFrame.gameObject.SetActive(false);
    }

    private void CreateHeader(RectTransform root)
    {
        turnText = CreateText(root, "Turn", "PLAYER TURN", 25, TextAnchor.MiddleCenter, Color.white);
        turnText.fontStyle = FontStyles.Bold;
        SetRect(turnText.rectTransform, new Vector2(0.5f, 0.945f), new Vector2(400f, 42f), Vector2.zero);

        timerText = CreateText(root, "Countdown", "10.0", 58, TextAnchor.MiddleCenter, Color.white);
        timerText.fontStyle = FontStyles.Bold;
        timerText.enableAutoSizing = true;
        timerText.fontSizeMin = 30;
        timerText.fontSizeMax = 58;
        SetRect(timerText.rectTransform, new Vector2(0.5f, 0.885f), new Vector2(190f, 78f), Vector2.zero);
    }

    private void CreateMappedFighterHud(Transform mappedPanel, Fighter fighter, bool isPlayer)
    {
        if (mappedPanel == null)
        {
            Debug.LogError($"Missing Hierarchy element for {fighter.Name} stats.");
            return;
        }

        Color accent = isPlayer
            ? new Color(0.22f, 0.72f, 1f)
            : new Color(1f, 0.27f, 0.30f);
        SpriteRenderer panelRenderer = mappedPanel.GetComponent<SpriteRenderer>();
        if (panelRenderer != null)
        {
            panelRenderer.color = isPlayer
                ? new Color(0.012f, 0.055f, 0.11f, 0.94f)
                : new Color(0.14f, 0.012f, 0.02f, 0.94f);
            panelRenderer.sortingOrder = 2;
        }

        GameObject canvasObject = new GameObject(
            fighter.Name + " Stats Element",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(mappedPanel, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30;

        RectTransform panel = canvasObject.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(288f, 205f);
        panel.localPosition = new Vector3(0f, 0f, -0.05f);
        panel.localRotation = Quaternion.identity;
        Vector3 scale = mappedPanel.lossyScale;
        panel.localScale = new Vector3(
            0.01f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            0.01f / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            1f);

        TMP_Text name = CreateText(panel, "Name", fighter.Name, 28, TextAnchor.MiddleCenter, accent);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(250f, 38f), Vector2.zero);

        TMP_Text stats = CreateText(panel, "Stats", "", 18, TextAnchor.MiddleCenter, Color.white);
        SetRect(stats.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(255f, 58f), Vector2.zero);
        if (isPlayer)
        {
            playerStatsText = stats;
        }
        else
        {
            cpuStatsText = stats;
        }

        RectTransform healthBar = CreatePanel(panel, "Health Bar", new Color(0.08f, 0.08f, 0.10f, 0.95f));
        SetRect(healthBar, new Vector2(0.5f, 0.66f), new Vector2(245f, 18f), Vector2.zero);
        Image fill = CreateImage(healthBar, "Fill", null, accent);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fill.type = Image.Type.Simple;
        if (isPlayer)
        {
            playerHealthFill = fill;
        }
        else
        {
            cpuHealthFill = fill;
        }

        List<TMP_Text> queueTexts = isPlayer ? playerPendingTexts : cpuPendingTexts;
        for (int i = 0; i < OrbNames.Length; i++)
        {
            float x = 0.12f + i * 0.19f;
            TMP_Text queue = CreateText(panel, OrbNames[i] + " Queue", "0", 16, TextAnchor.MiddleCenter, OrbColors[i]);
            queue.fontStyle = FontStyles.Bold;
            SetRect(queue.rectTransform, new Vector2(x, 0.12f), new Vector2(52f, 42f), Vector2.zero);
            queueTexts.Add(queue);
        }

        GameObject characterSlot = new GameObject(
            isPlayer ? "CharacterSlot_Player" : "CharacterSlot_Enemy",
            typeof(RectTransform));
        characterSlot.transform.SetParent(mappedPanel, false);
        characterSlot.transform.localPosition = new Vector3(0f, 1.55f, -0.02f);
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
        GameObject orbObject = new GameObject(
            $"Orb {row},{column}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        orbObject.transform.SetParent(boardRoot, false);

        Image image = orbObject.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = OrbColors[(int)type];
        image.preserveAspect = true;

        Button button = orbObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        OrbView orb = new OrbView
        {
            Type = type,
            Rect = orbObject.GetComponent<RectTransform>(),
            Image = image,
            Button = button,
            Row = row,
            Column = column
        };
        button.onClick.AddListener(() => OnOrbPointerClicked(orb));

        EventTrigger trigger = orbObject.AddComponent<EventTrigger>();
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
        if (battleEnded)
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
        if (!battleEnded && playerTurn && !boardBusy)
        {
            MoveFocusTo(0, 0);
        }
    }

    private void OnOrbPointerClicked(OrbView orb)
    {
        if (!inputReady)
        {
            messageText.text = "Press ENTER or GAMEPAD SOUTH to enable controls";
            return;
        }
        MoveFocusTo(orb.Row, orb.Column);
        SubmitFocusedOrb();
    }

    private void SubmitFocusedOrb()
    {
        if (!playerTurn || boardBusy || battleEnded)
        {
            return;
        }

        OrbView orb = board[cursorRow, cursorColumn];
        if (selectedOrb == null)
        {
            selectedOrb = orb;
            ShowFrameAt(lockedFrame, orb.Row, orb.Column);
            messageText.text = "Selected - move to an adjacent orb and press Action";
            return;
        }

        if (selectedOrb == orb)
        {
            selectedOrb = null;
            lockedFrame.gameObject.SetActive(false);
            messageText.text = "";
            return;
        }

        if (AreAdjacent(selectedOrb, orb))
        {
            OrbView first = selectedOrb;
            selectedOrb = null;
            lockedFrame.gameObject.SetActive(false);
            StartCoroutine(TrySwap(first, orb, true));
        }
        else
        {
            selectedOrb = orb;
            ShowFrameAt(lockedFrame, orb.Row, orb.Column);
            messageText.text = "Selection moved - choose an adjacent orb";
        }
    }

    private void PrepareForInput()
    {
        inputReady = false;
        boardBusy = false;
        timeRemaining = TurnDuration;
        turnText.text = "READY?";
        turnText.color = new Color(1f, 0.88f, 0.35f);
        timerText.text = "10";
        timerText.color = Color.white;
        messageText.text = "PRESS ENTER / SPACE / GAMEPAD SOUTH";
        hookText.text = "ENABLE CONTROLS TO BEGIN";
        MoveFocusTo(0, 0);
        lockedFrame.gameObject.SetActive(false);
        UpdateHud();
    }

    private void HandlePlayerNavigation()
    {
        if (mouseHoverOrb == null)
        {
            Vector2Int direction = ReadNavigationDirection();
            if (direction != Vector2Int.zero && Time.unscaledTime >= nextNavigationTime)
            {
                nextNavigationTime = Time.unscaledTime + 0.16f;
                MoveFocusTo(
                    Mathf.Clamp(cursorRow - direction.y, 0, Rows - 1),
                    Mathf.Clamp(cursorColumn + direction.x, 0, Columns - 1));
            }
        }

        if (SubmitPressed())
        {
            SubmitFocusedOrb();
        }
        else if (CancelPressed() && selectedOrb != null)
        {
            selectedOrb = null;
            lockedFrame.gameObject.SetActive(false);
            messageText.text = "Selection cancelled";
        }
    }

    private void MoveFocusTo(int row, int column)
    {
        cursorRow = row;
        cursorColumn = column;
        ShowFrameAt(focusFrame, row, column);
    }

    private static void ShowFrameAt(RectTransform frame, int row, int column)
    {
        float step = CellSize + CellGap;
        frame.anchoredPosition = new Vector2(
            (column - (Columns - 1) * 0.5f) * step,
            ((Rows - 1) * 0.5f - row) * step);
        frame.gameObject.SetActive(true);
    }

    private RectTransform CreateSelectionFrame(Transform parent, string name, Color color)
    {
        GameObject frameObject = new GameObject(name, typeof(RectTransform));
        frameObject.transform.SetParent(parent, false);
        RectTransform frame = frameObject.GetComponent<RectTransform>();
        SetRect(frame, new Vector2(0.5f, 0.5f), new Vector2(82f, 82f), Vector2.zero);

        Vector2[] anchors =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };
        for (int i = 0; i < 4; i++)
        {
            Sprite sprite = selectionSprites != null && selectionSprites.Length > i
                ? selectionSprites[i]
                : null;
            Image corner = CreateImage(frame, $"Corner {i}", sprite, color);
            corner.preserveAspect = true;
            corner.raycastTarget = false;
            RectTransform rect = corner.rectTransform;
            rect.anchorMin = anchors[i];
            rect.anchorMax = anchors[i];
            rect.pivot = anchors[i];
            rect.sizeDelta = new Vector2(24f, 24f);
            rect.anchoredPosition = Vector2.zero;
        }
        return frame;
    }

    private static bool SubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = Keyboard.current != null &&
                        (Keyboard.current.enterKey.wasPressedThisFrame ||
                         Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                         Keyboard.current.spaceKey.wasPressedThisFrame);
        bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return keyboard || gamepad;
#else
        return Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.Space) ||
               Input.GetButtonDown("Submit");
#endif
    }

    private static bool CancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return keyboard || gamepad;
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel");
#endif
    }

    private static Vector2Int ReadNavigationDirection()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) input.y += 1f;
        }
        if (Gamepad.current != null)
        {
            input += Gamepad.current.dpad.ReadValue();
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > 0.55f) input.x += Mathf.Sign(stick.x);
            if (Mathf.Abs(stick.y) > 0.55f) input.y += Mathf.Sign(stick.y);
        }
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) return new Vector2Int((int)Mathf.Sign(input.x), 0);
        if (Mathf.Abs(input.y) > 0.1f) return new Vector2Int(0, (int)Mathf.Sign(input.y));
        return Vector2Int.zero;
#else
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(x) > Mathf.Abs(y)) return new Vector2Int((int)Mathf.Sign(x), 0);
        if (Mathf.Abs(y) > 0.1f) return new Vector2Int(0, (int)Mathf.Sign(y));
        return Vector2Int.zero;
#endif
    }

    private IEnumerator TrySwap(OrbView first, OrbView second, bool showInvalidMessage)
    {
        boardBusy = true;
        SwapTypes(first, second);
        yield return AnimateSwap(first, second);

        HashSet<OrbView> matches = FindMatches();
        if (matches.Count == 0)
        {
            SwapTypes(first, second);
            yield return AnimateInvalidSwap(first, second);
            if (showInvalidMessage)
            {
                messageText.text = "No match - choose another move";
            }
            boardBusy = false;
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

        messageText.text = combo > 1 ? $"CHAIN x{combo}!" : "MATCH!";
        boardBusy = false;
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
        timeRemaining = 0f;
        UpdateTimer();

        Fighter acting = playerTurn ? player : cpu;
        Fighter target = playerTurn ? cpu : player;
        messageText.text = "DESTINY DECIDED!";
        hookText.text = "RESOLVING THE QUEUE...";
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

        messageText.text = BuildResolutionMessage(acting, damage, blocked, heal, shieldGain, manaGain, specialBursts);
        if (damage > 0)
        {
            Transform hitPanel = target == player ? elementMap.panelPlayer : elementMap.panelEnemy;
            yield return FlashDamage(hitPanel);
        }
        yield return new WaitForSeconds(1f);

        if (target.Health <= 0)
        {
            EndBattle(acting == player);
            yield break;
        }

        BeginTurn(!playerTurn);
        boardBusy = false;
    }

    private static IEnumerator FlashDamage(Transform targetPanel)
    {
        SpriteRenderer renderer = targetPanel != null ? targetPanel.GetComponent<SpriteRenderer>() : null;
        if (renderer == null)
        {
            yield break;
        }

        Color original = renderer.color;
        for (int i = 0; i < 4; i++)
        {
            renderer.color = i % 2 == 0
                ? new Color(1f, 0.14f, 0.12f, original.a)
                : original;
            yield return new WaitForSeconds(0.07f);
        }
        renderer.color = original;
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
        lockedFrame.gameObject.SetActive(false);
        if (isPlayer)
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
            focusFrame.gameObject.SetActive(false);
        }
        turnText.text = isPlayer ? "PLAYER TURN" : "CPU TURN";
        turnText.color = isPlayer
            ? new Color(0.22f, 0.72f, 1f)
            : new Color(1f, 0.27f, 0.30f);
        messageText.text = isPlayer ? "Match wisely. Build your queue before time runs out." : "CPU is planning...";
        hookText.text = isPlayer
            ? "RACE AGAINST TIME. MATCH WISELY. SURVIVE THE COUNTDOWN."
            : "EVERY 10 SECONDS, DESTINY IS DECIDED.";
        UpdateTimer();
        UpdateHud();
    }

    private void EndBattle(bool playerWon)
    {
        battleEnded = true;
        boardBusy = true;
        turnText.text = playerWon ? "VICTORY" : "DEFEAT";
        turnText.color = playerWon ? OrbColors[(int)OrbType.Yellow] : OrbColors[(int)OrbType.Purple];
        timerText.text = "0";
        messageText.text = playerWon ? "Destiny favors you." : "The CPU decided your fate.";
        hookText.text = "Press R / GAMEPAD NORTH to restart";
        StartCoroutine(RestartListener());
    }

    private IEnumerator RestartListener()
    {
        while (true)
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardRestart = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            bool gamepadRestart = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
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
        messageText.text = "No moves - board reshuffled!";
    }

    private void UpdateTimer()
    {
        float shownTime = Mathf.Max(0f, timeRemaining);
        timerText.text = shownTime > 3f ? shownTime.ToString("0.0") : Mathf.CeilToInt(shownTime).ToString();
        timerText.color = shownTime <= 3f
            ? OrbColors[(int)OrbType.Red]
            : new Color(1f, 0.94f, 0.72f);
        timerText.transform.localScale = shownTime <= 3f
            ? Vector3.one * (1f + Mathf.PingPong(Time.time * 3f, 0.12f))
            : Vector3.one;
    }

    private void UpdateHud()
    {
        playerStatsText.text = $"HP {player.Health}/100   MP {player.Mana}/30\nSH {player.Shield}   SP {player.Special}/12";
        cpuStatsText.text = $"HP {cpu.Health}/100   MP {cpu.Mana}/30\nSH {cpu.Shield}   SP {cpu.Special}/12";
        SetHealthBar(playerHealthFill, player.Health / 100f);
        SetHealthBar(cpuHealthFill, cpu.Health / 100f);

        string[] shortNames = { "ATK", "MP", "HP", "SH", "SP" };
        for (int i = 0; i < OrbNames.Length; i++)
        {
            playerPendingTexts[i].text = $"{shortNames[i]}\n{player.Pending[i]}";
            cpuPendingTexts[i].text = $"{shortNames[i]}\n{cpu.Pending[i]}";
            playerPendingTexts[i].color = player.Pending[i] > 0 ? OrbColors[i] : Color.white;
            cpuPendingTexts[i].color = cpu.Pending[i] > 0 ? OrbColors[i] : Color.white;
        }
    }

    private static void SetHealthBar(Image fill, float normalizedHealth)
    {
        normalizedHealth = Mathf.Clamp01(normalizedHealth);
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Max(0.03f, normalizedHealth), 1f);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        fill.enabled = normalizedHealth > 0f;
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

    private static void SetSelected(OrbView orb, bool selected)
    {
        orb.Rect.localScale = selected ? Vector3.one * 1.12f : Vector3.one;
        orb.Image.color = selected ? Color.Lerp(OrbColors[(int)orb.Type], Color.white, 0.35f) : OrbColors[(int)orb.Type];
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelObject.GetComponent<Image>().color = color;
        return panelObject.GetComponent<RectTransform>();
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = GameFontController.Font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = ConvertAlignment(alignment);
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = new Color32(0, 0, 0, 210);
        text.outlineWidth = 0.12f;
        return text;
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
