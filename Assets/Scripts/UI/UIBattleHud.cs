using TMPro;
using UnityEngine;

public class UIBattleHud : MonoBehaviour
{
    private const float CountdownFontSize = 82f;

    [SerializeField]
    private TMP_Text turnText;
    [SerializeField]
    private TMP_Text timerText;
    [SerializeField]
    private TMP_Text messageText;
    [SerializeField]
    private TMP_Text hookText;

    public void SetTurn(string text, Color color)
    {
        turnText.text = text;
        turnText.color = color;
    }

    public void SetTimer(string text, Color color, bool pulse)
    {
        ApplyCountdownStyle();
        timerText.text = text;
        timerText.color = color;
        timerText.transform.localScale = pulse
            ? Vector3.one * (1f + Mathf.PingPong(Time.time * 3f, 0.12f))
            : Vector3.one;
    }

    public void ApplyCountdownStyle()
    {
        TMP_FontAsset countdownFont = GameFontController.CountdownFont;
        if (countdownFont != null)
        {
            timerText.font = countdownFont;
        }
        timerText.enableAutoSizing = false;
        timerText.fontSize = CountdownFontSize;
        timerText.rectTransform.sizeDelta = new Vector2(240f, 100f);
    }

    public void SetMessage(string text)
    {
        messageText.text = text;
    }

    public void SetHook(string text)
    {
        hookText.text = text;
    }
}
