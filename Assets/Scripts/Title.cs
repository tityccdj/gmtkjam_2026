using UnityEngine;

public class Title : MonoBehaviour
{
    [SerializeField]
    private UIMainMenu uiMainMenu;
    [SerializeField]
    private UISetting uiSetting;
    [SerializeField]
    private UITutorial uiTutorial;
    [SerializeField]
    private UIMode uiMode;

    void Start()
    {
        AudioManager.Instance.PlayMusic("title", 0.8f, true);

        uiMainMenu.Setup(new UIMainMenu.Param
        {
            onPlay = () =>
            {
                uiMainMenu.gameObject.SetActive(false);
                uiMode.SlideIn();
            },
            onSetting = () =>
            {
                uiMainMenu.gameObject.SetActive(false);
                uiSetting.gameObject.SetActive(true);
            },
            onTutorial = () =>
            {
                uiMainMenu.gameObject.SetActive(false);
                uiTutorial.gameObject.SetActive(true);
            },
            onExit = () => Application.Quit(),
            version = Application.version,
        });
        uiMode.Setup(new UIMode.Param
        {
            onStoryMode = () => Debug.Log("Story Mode selected"),
            onLocalVersus = () => Debug.Log("Local Versus selected"),
            onFreeplay = () => Debug.Log("Freeplay selected"),
            onBack = () => uiMode.SlideOut(() => uiMainMenu.gameObject.SetActive(true)),
        });
        uiSetting.Setup(new UISetting.Param
        {
            mainVolume = AudioManager.Instance.GetMasterVolume(),
            bgmVolume = AudioManager.Instance.GetMusicVolume(),
            sfxVolume = AudioManager.Instance.GetSFXVolume(),
            onMainVolumeChanged = value => AudioManager.Instance.SetMasterVolume(value),
            onBgmVolumeChanged = value => AudioManager.Instance.SetMusicVolume(value),
            onSfxVolumeChanged = value => AudioManager.Instance.SetSFXVolume(value),
            onBack = () =>
            {
                uiMainMenu.gameObject.SetActive(true);
                uiSetting.gameObject.SetActive(false);
            },
        });
        uiTutorial.Setup(new UITutorial.Param
        {
            OnBack = () =>
            {
                uiMainMenu.gameObject.SetActive(true);
                uiTutorial.gameObject.SetActive(false);
            }
        });
        uiMainMenu.gameObject.SetActive(true);
        uiSetting.gameObject.SetActive(false);
        uiTutorial.gameObject.SetActive(false);
        uiMode.gameObject.SetActive(false);
    }
}
