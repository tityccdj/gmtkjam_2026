using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITutorialHint : MonoBehaviour
{
    [SerializeField]
    private TMP_Text titleText;
    [SerializeField]
    private TMP_Text bodyText;
    [SerializeField]
    private Button dismissButton;

    private Action onDismiss;

    private void Awake()
    {
        dismissButton.AddClickSound();
        dismissButton.onClick.AddListener(Dismiss);
        Hide();
    }

    public void Show(string title, string body, Action onDismissed)
    {
        onDismiss = onDismissed;
        titleText.text = title;
        bodyText.text = body;
        gameObject.SetActive(true);
        dismissButton.SelectForNavigation();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Dismiss()
    {
        Action callback = onDismiss;
        onDismiss = null;
        Hide();
        callback?.Invoke();
    }
}
