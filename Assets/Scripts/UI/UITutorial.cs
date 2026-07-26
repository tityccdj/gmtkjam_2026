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
    private Button backButton;
    
    public void Setup(Param param)
    {
        backButton.AddClickSound();
        backButton.onClick.AddListener(() => param.OnBack?.Invoke());
    }
}
