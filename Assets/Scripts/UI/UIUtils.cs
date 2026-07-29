using UnityEngine;
using UnityEngine.EventSystems;
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

    /// <summary>
    /// Gives keyboard/gamepad navigation a valid starting point after a panel
    /// is opened. Mouse input remains available and can change the selection.
    /// </summary>
    public static void SelectForNavigation(this Selectable selectable)
    {
        if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable())
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        EnsureSelectionFeedback(selectable);
        Canvas.ForceUpdateCanvases();
        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(selectable.gameObject);
    }

    public static void SelectFirstInteractable(Transform root, Selectable fallback = null)
    {
        if (root != null)
        {
            EnsureSelectionFeedback(root);
            Canvas.ForceUpdateCanvases();
            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(false);
            foreach (Selectable selectable in selectables)
            {
                if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                {
                    selectable.SelectForNavigation();
                    return;
                }
            }
        }

        fallback.SelectForNavigation();
    }

    public static void EnsureSelectionFeedback(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            EnsureSelectionFeedback(selectable);
        }
    }

    private static void EnsureSelectionFeedback(Selectable selectable)
    {
        if (selectable == null ||
            selectable.GetComponent<ButtonAnimation>() != null ||
            selectable.GetComponent<UISelectionFeedback>() != null)
        {
            return;
        }

        selectable.gameObject.AddComponent<UISelectionFeedback>();
    }
}

[DisallowMultipleComponent]
public sealed class UISelectionFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private const float SelectedScale = 1.08f;
    private const float Duration = 0.16f;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnSelect(BaseEventData eventData)
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * SelectedScale, Duration)
            .setEase(LeanTweenType.easeOutQuad);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale, Duration)
            .setEase(LeanTweenType.easeOutQuad);
    }

    private void OnDisable()
    {
        LeanTween.cancel(gameObject);
        transform.localScale = originalScale;
    }
}
