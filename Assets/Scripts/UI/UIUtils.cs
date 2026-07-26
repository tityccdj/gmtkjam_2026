using UnityEngine;
using UnityEngine.UI;

public static class UIUtils
{
    public static void AddClickSound(this Button button)
    {
        if (button != null)
        {
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFXOneShot("UI_click");
                }
            });
        }
    }
}
