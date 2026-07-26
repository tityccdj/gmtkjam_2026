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
    [SerializeField]
    private GameObject specialPanel;
    [SerializeField]
    private Image specialImage;

    public void SetName(string text)
    {
        nameText.text = text;
    }

    public void SetStats(string text)
    {
        statsText.text = text;
    }

    private float targetHealth = 1f;
    private float currentHealth = 1f;
    private bool initialized = false;

    public void SetHealth(float normalizedHealth)
    {
        targetHealth = Mathf.Clamp01(normalizedHealth);
        if (!initialized)
        {
            currentHealth = targetHealth;
            initialized = true;
            ApplyHealthVisuals(currentHealth);
        }
    }

    private void Update()
    {
        if (initialized && Mathf.Abs(currentHealth - targetHealth) > 0.0001f)
        {
            currentHealth = Mathf.Lerp(currentHealth, targetHealth, Time.deltaTime * 5f);
            ApplyHealthVisuals(currentHealth);
        }
    }

    private void ApplyHealthVisuals(float normalizedHealth)
    {
        RectTransform rect = healthFill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Max(0.03f, normalizedHealth), 1f);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        healthFill.enabled = normalizedHealth > 0f;
    }

    private Coroutine[] pendingCoroutines;
    private string[] previousPendingTexts;

    private void Awake()
    {
        if (pendingTexts != null)
        {
            pendingCoroutines = new Coroutine[pendingTexts.Length];
            previousPendingTexts = new string[pendingTexts.Length];
        }

        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
        }
    }

    public void SetPending(int index, string text, Color color)
    {
        if (pendingTexts == null || index < 0 || index >= pendingTexts.Length) return;

        bool valueChanged = previousPendingTexts[index] != text;
        previousPendingTexts[index] = text;
        pendingTexts[index].text = text;
        pendingTexts[index].color = color;

        bool isZero = color == Color.white;
        if (valueChanged && !isZero && gameObject.activeInHierarchy)
        {
            if (pendingCoroutines[index] != null)
            {
                StopCoroutine(pendingCoroutines[index]);
            }
            pendingCoroutines[index] = StartCoroutine(AnimatePendingText(index));
        }
    }

    private IEnumerator AnimatePendingText(int index)
    {
        Transform textTransform = pendingTexts[index].transform;
        float elapsed = 0f;
        float duration = 0.25f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.4f;
            textTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        textTransform.localScale = Vector3.one;
        pendingCoroutines[index] = null;
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

    public IEnumerator ShowSpecialPanel(Sprite specialSprite = null)
    {
        if (specialPanel != null)
        {
            if (specialImage != null && specialSprite != null)
            {
                specialImage.sprite = specialSprite;
            }
            
            Transform panelTransform = specialPanel.transform;
            panelTransform.localScale = Vector3.zero;
            specialPanel.SetActive(true);
            
            float elapsed = 0f;
            float durationIn = 0.35f;
            float c1 = 1.70158f;
            float c3 = c1 + 1f;

            while (elapsed < durationIn)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / durationIn;
                float tMinus1 = t - 1f;
                float scale = 1f + c3 * (tMinus1 * tMinus1 * tMinus1) + c1 * (tMinus1 * tMinus1);
                panelTransform.localScale = Vector3.one * Mathf.Max(0f, scale);
                yield return null;
            }
            panelTransform.localScale = Vector3.one;

            yield return new WaitForSeconds(1.4f);

            float elapsedOut = 0f;
            float durationOut = 0.25f;
            while (elapsedOut < durationOut)
            {
                elapsedOut += Time.deltaTime;
                float t = elapsedOut / durationOut;
                float scale = c3 * (t * t * t) - c1 * (t * t);
                panelTransform.localScale = Vector3.one * Mathf.Max(0f, 1f - scale);
                yield return null;
            }
            
            specialPanel.SetActive(false);
            panelTransform.localScale = Vector3.one;
        }
    }
}
