using System;
using UnityEngine;
using UnityEngine.UI;

public class UITutorial : MonoBehaviour
{
    public struct Param
    {
        public Action OnBack;
    }

    [SerializeField]
    private Sprite[] tutorialSprites;
    [SerializeField]
    private Image image;
    [SerializeField]
    private Button nextButton;
    [SerializeField]
    private Button prevButton;
    [SerializeField]
    private Button backButton;
    private int currentStep = 0; // Current tutorial step index
    
    public void Setup(Param param)
    {
        nextButton.onClick.AddListener(OnNext);
        prevButton.onClick.AddListener(OnPrev);
        backButton.onClick.AddListener(() => param.OnBack?.Invoke());
        UpdateTutorialUI();
    }

    private void OnNext()
    {
        if (currentStep < tutorialSprites.Length - 1)
        {
            currentStep++;
            UpdateTutorialUI();
        }
    }

    private void OnPrev()
    {
        if (currentStep > 0)
        {
            currentStep--;
            UpdateTutorialUI();
        }
    }

    private void UpdateTutorialUI()
    {
        image.sprite = tutorialSprites[currentStep];
        prevButton.gameObject.SetActive(currentStep > 0);
        nextButton.gameObject.SetActive(currentStep < tutorialSprites.Length - 1);
    }
}
