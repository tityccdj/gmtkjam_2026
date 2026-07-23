using TMPro;
using UnityEngine;

public class UIBattleHud : MonoBehaviour
{
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
        timerText.text = text;
        timerText.color = color;
        timerText.transform.localScale = pulse
            ? Vector3.one * (1f + Mathf.PingPong(Time.time * 3f, 0.12f))
            : Vector3.one;
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
