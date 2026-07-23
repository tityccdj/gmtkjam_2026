using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIMainMenu : MonoBehaviour
{
    public struct Param
    {
        public string version;
        public Action onPlay;
        public Action onSetting;
        public Action onTutorial;
        public Action onExit;
    }

    [SerializeField]
    private Button playButton;
    [SerializeField]
    private Button settingButton;
    [SerializeField]
    private Button tutorialButton;
    [SerializeField]
    private TMP_Text versionText;

    public void Setup(Param param)
    {
        playButton.onClick.AddListener(() => param.onPlay?.Invoke());
        settingButton.onClick.AddListener(() => param.onSetting?.Invoke());
        tutorialButton.onClick.AddListener(() => param.onTutorial?.Invoke());
        versionText.text = $"V {param.version}";
    }
}
