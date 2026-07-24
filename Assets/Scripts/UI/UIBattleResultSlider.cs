using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIBattleResultSlider : MonoBehaviour
{
    [SerializeField] private Slider resultSlider;
    [SerializeField] private Image resultImage;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void ShowResult(bool playerWon)
    {
        gameObject.SetActive(true);
        
        string spritePath = playerWon ? "ui/GameScene/GameComplete" : "ui/GameScene/GameOver";
        Sprite loadedSprite = Resources.Load<Sprite>(spritePath);
        
        if (resultImage != null && loadedSprite != null)
        {
            resultImage.sprite = loadedSprite;
        }

        if (resultSlider != null)
        {
            StartCoroutine(AnimateSlider());
        }
    }

    private IEnumerator AnimateSlider()
    {
        resultSlider.value = 0f;
        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            resultSlider.value = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        resultSlider.value = 1f;
    }
}
