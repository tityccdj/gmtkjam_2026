using System;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SlidePanel : MonoBehaviour
{
    [SerializeField]
    private float slideDuration = 0.4f;
    [SerializeField]
    private LeanTweenType slideEase = LeanTweenType.easeOutCubic;

    private RectTransform rectTransform;
    private Vector2 onscreenPosition;
    private Vector2 offscreenPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        onscreenPosition = rectTransform.anchoredPosition;
        offscreenPosition = onscreenPosition - new Vector2(rectTransform.rect.width, 0);
    }

    public void SlideIn()
    {
        gameObject.SetActive(true);
        rectTransform.anchoredPosition = offscreenPosition;
        LeanTween.cancel(gameObject);
        LeanTween.move(rectTransform, onscreenPosition, slideDuration).setEase(slideEase);
    }

    public void SlideOut(Action onComplete = null)
    {
        LeanTween.cancel(gameObject);
        LeanTween.move(rectTransform, offscreenPosition, slideDuration).setEase(slideEase).setOnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}
