using System;
using UnityEngine;
using UnityEngine.UI;

public class UIStoryLevel : MonoBehaviour
{
    public struct Param
    {
        public Action onBack;
        public Action<LevelConfig> onLevelSelected;
    }

    [SerializeField]
    private SlidePanel slidePanel;
    [SerializeField]
    private Button backButton;
    [SerializeField]
    private Transform levelsContainer;
    [SerializeField]
    private GameObject unlockedTemplate;
    [SerializeField]
    private GameObject lockedTemplate;
    [SerializeField]
    private LevelConfig[] levels;

    public void Setup(Param param)
    {
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
        PopulateLevels(param.onLevelSelected);
    }

    private void PopulateLevels(Action<LevelConfig> onLevelSelected)
    {
        for (int i = levelsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(levelsContainer.GetChild(i).gameObject);
        }

        for (int i = 0; i < levels.Length; i++)
        {
            LevelConfig level = levels[i];
            bool unlocked = LevelSaveState.IsUnlocked(level, defaultUnlocked: i == 0);
            GameObject template = unlocked ? unlockedTemplate : lockedTemplate;
            GameObject entry = Instantiate(template, levelsContainer);
            entry.SetActive(true);

            Button button = entry.GetComponent<Button>();
            button.interactable = unlocked;
            if (unlocked)
            {
                button.onClick.AddListener(() => onLevelSelected?.Invoke(level));
            }
        }
    }

    public void SlideIn() => slidePanel.SlideIn();

    public void SlideOut(Action onComplete = null) => slidePanel.SlideOut(onComplete);
}
