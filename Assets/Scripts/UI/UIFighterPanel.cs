using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFighterPanel : MonoBehaviour
{
    [SerializeField]
    private TMP_Text nameText;
    [SerializeField]
    private TMP_Text statsText;
    [SerializeField]
    private Image healthFill;
    [SerializeField]
    private TMP_Text[] pendingTexts;
    [SerializeField]
    private SpriteRenderer arenaSprite;

    public void SetName(string text)
    {
        nameText.text = text;
    }

    public void SetStats(string text)
    {
        statsText.text = text;
    }

    public void SetHealth(float normalizedHealth)
    {
        normalizedHealth = Mathf.Clamp01(normalizedHealth);
        RectTransform rect = healthFill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Max(0.03f, normalizedHealth), 1f);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        healthFill.enabled = normalizedHealth > 0f;
    }

    public void SetPending(int index, string text, Color color)
    {
        pendingTexts[index].text = text;
        pendingTexts[index].color = color;
    }

    public IEnumerator Flash(Color flashColor)
    {
        if (arenaSprite == null)
        {
            yield break;
        }

        Color original = arenaSprite.color;
        for (int i = 0; i < 4; i++)
        {
            arenaSprite.color = i % 2 == 0 ? flashColor : original;
            yield return new WaitForSeconds(0.07f);
        }
        arenaSprite.color = original;
    }
}
