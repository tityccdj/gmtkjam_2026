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
    private SlidePanel slidePanel;
    [SerializeField]
    private Button storyModeButton;
    [SerializeField]
    private Button localVersusButton;
    [SerializeField]
    private Button freeplayButton;
    [SerializeField]
    private Button backButton;

    public void Setup(Param param)
    {
        storyModeButton.AddClickSound();
        storyModeButton.onClick.AddListener(() => param.onStoryMode?.Invoke());
        localVersusButton.AddClickSound();
        localVersusButton.onClick.AddListener(() => param.onLocalVersus?.Invoke());
        freeplayButton.AddClickSound();
        freeplayButton.onClick.AddListener(() => param.onFreeplay?.Invoke());
        backButton.AddClickSound();
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
    }

    public void SlideIn()
    {
        slidePanel.SlideIn();
        storyModeButton.SelectForNavigation();
    }

    public void SlideOut(Action onComplete = null) => slidePanel.SlideOut(onComplete);
}
