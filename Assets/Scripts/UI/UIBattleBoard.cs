using UnityEngine;
using UnityEngine.UI;

public class UIBattleBoard : MonoBehaviour
{
    private const float BoardPadding = 13f;
    private const float MinCellSize = 26f;
    private const float MaxCellSize = 78f;

    [SerializeField]
    private RectTransform boardShell;
    [SerializeField]
    private RectTransform boardRoot;
    [SerializeField]
    private GameObject orbCellPrefab;
    [SerializeField]
    private RectTransform selectionFrame;
    [SerializeField]
    private Image[] selectionCorners;
    [SerializeField]
    private float maxBoardPixelSize = 482f;
    [SerializeField]
    private float cellGap = 5f;

    public float CellStep { get; private set; }

    public float ConfigureGrid(int rows, int columns)
    {
        int maxDimension = Mathf.Max(rows, columns);
        float cellSize = Mathf.Clamp(
            (maxBoardPixelSize - BoardPadding * 2f - cellGap * (maxDimension - 1)) / maxDimension,
            MinCellSize,
            MaxCellSize);
        CellStep = cellSize + cellGap;

        float boardWidth = columns * cellSize + cellGap * (columns - 1) + BoardPadding * 2f;
        float boardHeight = rows * cellSize + cellGap * (rows - 1) + BoardPadding * 2f;
        boardShell.sizeDelta = new Vector2(boardWidth, boardHeight);
        boardRoot.sizeDelta = new Vector2(boardWidth, boardHeight);

        GridLayoutGroup grid = boardRoot.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(cellGap, cellGap);
        grid.padding = new RectOffset((int)BoardPadding, (int)BoardPadding, (int)BoardPadding, (int)BoardPadding);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        float frameSize = cellSize + 10f;
        selectionFrame.sizeDelta = new Vector2(frameSize, frameSize);
        float cornerSize = Mathf.Clamp(cellSize * 0.33f, 16f, 24f);
        foreach (Image corner in selectionCorners)
        {
            corner.rectTransform.sizeDelta = new Vector2(cornerSize, cornerSize);
        }

        return cellSize;
    }

    public RectTransform SpawnCell()
    {
        GameObject instance = Instantiate(orbCellPrefab, boardRoot);
        return instance.GetComponent<RectTransform>();
    }

    public void ShowSelectionAt(Vector2 anchoredPosition, Color color)
    {
        selectionFrame.anchoredPosition = anchoredPosition;
        selectionFrame.gameObject.SetActive(true);
        foreach (Image corner in selectionCorners)
        {
            corner.color = color;
        }
    }

    public void HideSelection()
    {
        selectionFrame.gameObject.SetActive(false);
    }
}
