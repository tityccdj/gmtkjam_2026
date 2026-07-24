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
        storyModeButton.onClick.AddListener(() => param.onStoryMode?.Invoke());
        localVersusButton.onClick.AddListener(() => param.onLocalVersus?.Invoke());
        freeplayButton.onClick.AddListener(() => param.onFreeplay?.Invoke());
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
    }

    public void SlideIn() => slidePanel.SlideIn();

    public void SlideOut(Action onComplete = null) => slidePanel.SlideOut(onComplete);
}
