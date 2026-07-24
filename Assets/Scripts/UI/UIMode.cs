using System;
using UnityEngine;
using UnityEngine.UI;

public class UIMode : MonoBehaviour
{
    public struct Param
    {
        public Action onStoryMode;
        public Action onLocalVersus;
        public Action onFreeplay;
        public Action onBack;
    }

    [SerializeField]
    private RectTransform panelRect;
    [SerializeField]
    private Button storyModeButton;
    [SerializeField]
    private Button localVersusButton;
    [SerializeField]
    private Button freeplayButton;
    [SerializeField]
    private Button backButton;
    [SerializeField]
    private float slideDuration = 0.4f;
    [SerializeField]
    private LeanTweenType slideEase = LeanTweenType.easeOutCubic;

    private Vector2 onscreenPosition;
    private Vector2 offscreenPosition;

    void Awake()
    {
        onscreenPosition = panelRect.anchoredPosition;
        offscreenPosition = onscreenPosition - new Vector2(panelRect.rect.width, 0);
    }

    public void Setup(Param param)
    {
        storyModeButton.onClick.AddListener(() => param.onStoryMode?.Invoke());
        localVersusButton.onClick.AddListener(() => param.onLocalVersus?.Invoke());
        freeplayButton.onClick.AddListener(() => param.onFreeplay?.Invoke());
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
    }

    public void SlideIn()
    {
        gameObject.SetActive(true);
        panelRect.anchoredPosition = offscreenPosition;
        LeanTween.cancel(gameObject);
        LeanTween.move(panelRect, onscreenPosition, slideDuration).setEase(slideEase);
    }

    public void SlideOut(Action onComplete = null)
    {
        LeanTween.cancel(gameObject);
        LeanTween.move(panelRect, offscreenPosition, slideDuration).setEase(slideEase).setOnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}
