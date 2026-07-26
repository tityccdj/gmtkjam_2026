using System;
using UnityEngine;
using UnityEngine.UI;

public class UIStoryLevel : MonoBehaviour
{
    public struct Param
    {
        public Action onBack;
        public Action<LevelConfig, int> onLevelSelected;
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

    public LevelConfig[] Levels => levels;

    public void Setup(Param param)
    {
        backButton.AddClickSound();
        backButton.onClick.AddListener(() => param.onBack?.Invoke());
        PopulateLevels(param.onLevelSelected);
    }

    private void PopulateLevels(Action<LevelConfig, int> onLevelSelected)
    {
        for (int i = levelsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(levelsContainer.GetChild(i).gameObject);
        }

        for (int i = 0; i < levels.Length; i++)
        {
            LevelConfig level = levels[i];
            int index = i;
            bool unlocked = LevelSaveState.IsUnlocked(level, defaultUnlocked: i == 0);
            GameObject template = unlocked ? unlockedTemplate : lockedTemplate;
            var thumbnail = template.transform.Find("Thumbnail")?.GetComponent<Image>();
            if (thumbnail != null)
            {
                thumbnail.sprite = level.thumbnail;
            }
            GameObject entry = Instantiate(template, levelsContainer);
            entry.SetActive(true);

            Button button = entry.GetComponent<Button>();
            button.interactable = unlocked;
            if (unlocked)
            {
                button.AddClickSound();
                button.onClick.AddListener(() => onLevelSelected?.Invoke(level, index));
            }
        }
    }

    public void SlideIn() => slidePanel.SlideIn();

    public void SlideOut(Action onComplete = null) => slidePanel.SlideOut(onComplete);
}
