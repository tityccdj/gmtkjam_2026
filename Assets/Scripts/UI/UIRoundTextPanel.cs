using System.Collections;
using TMPro;
using UnityEngine;

public class UIRoundTextPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text roundText;
    
    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void ShowRoundText(bool playerWon)
    {
        gameObject.SetActive(true);
        string targetText = playerWon ? "Round \ncomplete" : "Round \nfail";
        StartCoroutine(TypewriterEffect(targetText));
    }

    private IEnumerator TypewriterEffect(string text)
    {
        roundText.text = "";
        float timePerChar = 3f / text.Length;
        
        for (int i = 0; i < text.Length; i++)
        {
            roundText.text += text[i];
            yield return new WaitForSeconds(timePerChar);
        }
    }
}
