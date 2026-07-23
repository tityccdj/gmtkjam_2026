using UnityEngine;
using UnityEngine.UI;

public class UIBattleBoard : MonoBehaviour
{
    [SerializeField]
    private RectTransform boardRoot;
    [SerializeField]
    private GameObject orbCellPrefab;
    [SerializeField]
    private RectTransform selectionFrame;
    [SerializeField]
    private Image[] selectionCorners;

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
