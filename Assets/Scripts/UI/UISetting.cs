using System;
using UnityEngine;
using UnityEngine.UI;

public class UISetting : MonoBehaviour
{
    public struct Param
    {
        public float mainVolume;
        public float bgmVolume;
        public float sfxVolume;
        public Action<float> onMainVolumeChanged;
        public Action<float> onBgmVolumeChanged; 
        public Action<float> onSfxVolumeChanged;
        public Action onBack;
    }

    [SerializeField]
    private Slider mainVolumeSlider;
    [SerializeField]
    private Slider bgmVolumeSlider;
    [SerializeField]
    private Slider sfxVolumeSlider;
    [SerializeField]
    private Button backButton;

    public void Setup(Param param)
    {
        mainVolumeSlider.value = param.mainVolume;
        bgmVolumeSlider.value = param.bgmVolume;
        sfxVolumeSlider.value = param.sfxVolume;

        mainVolumeSlider.onValueChanged.AddListener(value => param.onMainVolumeChanged?.Invoke(value));
        bgmVolumeSlider.onValueChanged.AddListener(value => param.onBgmVolumeChanged?.Invoke(value));
        sfxVolumeSlider.onValueChanged.AddListener(value => param.onSfxVolumeChanged?.Invoke(value));
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
    }
}