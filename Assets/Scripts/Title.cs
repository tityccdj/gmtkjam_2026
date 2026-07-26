using UnityEngine;

public class Title : MonoBehaviour
{
    // Where the character selection panel sends the player once a character is picked.
    private enum CharacterSelectFlow
    {
        Story,
        FreePlay,
        LocalVersus
    }

    [SerializeField]
    private string gameScene;
    [SerializeField]
    private UIMainMenu uiMainMenu;
    [SerializeField]
    private UISetting uiSetting;
    [SerializeField]
    private UITutorial uiTutorial;
    [SerializeField]
    private UIMode uiMode;
    [SerializeField]
    private UICharacterSelect uiCharacterSelect;
    [SerializeField]
    private UIStoryLevel uiStoryLevel;
    [SerializeField]
    private UIStoryIntro uiStoryIntro;

    private CharacterSelectFlow characterSelectFlow;

    void Start()
    {
        AudioManager.Instance.PlayMusic("title", 0.8f, true);

        uiMainMenu.Setup(new UIMainMenu.Param
        {
            onPlay = () => uiMainMenu.SlideOut(() => uiMode.SlideIn()),
            onSetting = () => uiMainMenu.SlideOut(() => uiSetting.gameObject.SetActive(true)),
            onTutorial = () => uiMainMenu.SlideOut(() => uiTutorial.gameObject.SetActive(true)),
            onExit = () => Application.Quit(),
            version = Application.version,
        });
        uiMode.Setup(new UIMode.Param
        {
            onStoryMode = () => OpenCharacterSelect(CharacterSelectFlow.Story),
            onLocalVersus = () => OpenCharacterSelect(CharacterSelectFlow.LocalVersus),
            onFreeplay = () => OpenCharacterSelect(CharacterSelectFlow.FreePlay),
            onBack = () => uiMode.SlideOut(() => uiMainMenu.SlideIn()),
        });
        uiCharacterSelect.Setup(new UICharacterSelect.Param
        {
            onBack = () => uiCharacterSelect.SlideOut(() => uiMode.SlideIn()),
            onCharacterSelected = (character, index) =>
            {
                LevelSelection.PlayerCharacter = character;
                // Story mode leaves the opponent to the level config; the other
                // two modes hand the remaining character to the other side.
                LevelSelection.OpponentCharacter = characterSelectFlow == CharacterSelectFlow.Story
                    ? null
                    : uiCharacterSelect.GetOpponentOf(index);

                if (characterSelectFlow == CharacterSelectFlow.Story)
                {
                    uiCharacterSelect.SlideOut(() => uiStoryLevel.SlideIn());
                    return;
                }

                ClearStorySelection();
                LevelSelection.PlayerVsPlayer = characterSelectFlow == CharacterSelectFlow.LocalVersus;
                LevelSelection.GameMode = characterSelectFlow == CharacterSelectFlow.LocalVersus
                    ? ProceduralMatchFighter.BattleGameMode.Story
                    : ProceduralMatchFighter.BattleGameMode.FreePlay;
                SceneLoader.Instance.LoadScene(gameScene);
            },
        });
        uiStoryLevel.Setup(new UIStoryLevel.Param
        {
            onBack = () => uiStoryLevel.SlideOut(() => uiCharacterSelect.SlideIn()),
            onLevelSelected = (level, index) =>
            {
                LevelSelection.Current = level;
                LevelSelection.CurrentIndex = index;
                LevelSelection.AllLevels = uiStoryLevel.Levels;
                LevelSelection.PlayerVsPlayer = false;
                LevelSelection.GameMode = ProceduralMatchFighter.BattleGameMode.Story;
                
                if (index == 0 && uiStoryIntro != null)
                {
                    uiStoryIntro.Show(() => SceneLoader.Instance.LoadScene(gameScene));
                }
                else
                {
                    SceneLoader.Instance.LoadScene(gameScene);
                }
            },
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
                uiSetting.gameObject.SetActive(false);
                uiMainMenu.SlideIn();
            },
        });
        uiTutorial.Setup(new UITutorial.Param
        {
            OnBack = () =>
            {
                uiTutorial.gameObject.SetActive(false);
                uiMainMenu.SlideIn();
            }
        });
        uiSetting.gameObject.SetActive(false);
        uiTutorial.gameObject.SetActive(false);
        uiMode.gameObject.SetActive(false);
        uiCharacterSelect.gameObject.SetActive(false);
        uiStoryLevel.gameObject.SetActive(false);
        uiMainMenu.SlideIn();
    }

    private void OpenCharacterSelect(CharacterSelectFlow flow)
    {
        characterSelectFlow = flow;
        uiCharacterSelect.SetTitle(flow == CharacterSelectFlow.LocalVersus
            ? "PLAYER 1 - SELECT YOUR CHARACTER"
            : "SELECT YOUR CHARACTER");
        uiMode.SlideOut(() => uiCharacterSelect.SlideIn());
    }

    // The selection statics survive scene loads, so a story level picked earlier
    // must not leak into a free play / versus match started afterwards.
    private void ClearStorySelection()
    {
        LevelSelection.Current = null;
        LevelSelection.AllLevels = null;
        LevelSelection.CurrentIndex = -1;
    }
}
