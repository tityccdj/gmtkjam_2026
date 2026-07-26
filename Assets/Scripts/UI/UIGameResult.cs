using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIGameResult : MonoBehaviour
{
    public struct Param
    {
        public Action onNextLevel;
        public Action onTryAgain;
        public Action onMainMenu;
    }

    [Header("Buttons")]
    [SerializeField]
    private Button nextLevelButton;
    [SerializeField]
    private Button tryAgainButton;
    [SerializeField]
    private Button mainMenuButton;

    public void Setup(Param param)
    {

        // ล้าง Event เก่าออกก่อนเพื่อป้องกันการทำงานซ้ำซ้อน
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveAllListeners();
        if (tryAgainButton != null) tryAgainButton.onClick.RemoveAllListeners();
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveAllListeners();

        // ผูก Action ให้กับปุ่ม
        if (nextLevelButton != null)
        {
            nextLevelButton.AddClickSound();
            nextLevelButton.onClick.AddListener(() => param.onNextLevel?.Invoke());
            // แสดงปุ่ม Next Level เฉพาะเมื่อมีการส่ง Action มา (ถ้าแพ้เกมจะไม่มีปุ่ม Next Level)
            nextLevelButton.gameObject.SetActive(param.onNextLevel != null);
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.AddClickSound();
            tryAgainButton.onClick.AddListener(() => param.onTryAgain?.Invoke());
            tryAgainButton.gameObject.SetActive(param.onTryAgain != null);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.AddClickSound();
            mainMenuButton.onClick.AddListener(() => param.onMainMenu?.Invoke());
            mainMenuButton.gameObject.SetActive(param.onMainMenu != null);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
