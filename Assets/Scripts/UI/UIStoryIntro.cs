using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIStoryIntro : MonoBehaviour, IPointerClickHandler
{
    private Action onCompleteCallback;
    
    [Header("Panels Configuration")]
    [Tooltip("ใส่ Panel ย่อยเรียงตามลำดับ 1-6")]
    [SerializeField] private RectTransform[] panels;
    
    // ตั้งระยะห่างให้พอที่จะอยู่นอกจอแน่นอน
    private float offsetDistance = 2500f;

    private Vector2[] originalPositions;
    private int currentPanelIndex = 0;
    private bool isAnimating = false;
    private bool isReadyToDismiss = false;

    private void Awake()
    {
        // บันทึกตำแหน่งเดิมของแต่ละ panel เพื่อให้มันกลับมาที่ตำแหน่งที่จัดไว้ใน Editor ได้
        if (panels != null)
        {
            originalPositions = new Vector2[panels.Length];
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null)
                {
                    originalPositions[i] = panels[i].anchoredPosition;
                }
            }
        }
    }

    public void Show(Action onComplete)
    {
        onCompleteCallback = onComplete;
        gameObject.SetActive(true);
        currentPanelIndex = 0;
        isReadyToDismiss = false;
        isAnimating = false;

        // นำทุก panel ไปซ่อนไว้ตามทิศทางที่กำหนด
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
            {
                Vector2 startOffset = GetStartOffset(i);
                panels[i].anchoredPosition = originalPositions[i] + startOffset;
                panels[i].gameObject.SetActive(false);
            }
        }

        // เริ่มโชว์ Panel แรก (ลำดับ 0) ทันทีที่เข้า Intro
        ShowNextPanel();
    }

    private Vector2 GetStartOffset(int index)
    {
        // Panel 1: บน, Panel 2: ซ้าย, Panel 3: บน, Panel 4: ขวา, Panel 5: ล่าง, Panel 6: ขวา
        switch (index)
        {
            case 0: return new Vector2(0, offsetDistance);   // 1 จากบน
            case 1: return new Vector2(-offsetDistance, 0);  // 2 จากซ้าย
            case 2: return new Vector2(0, offsetDistance);   // 3 จากบน
            case 3: return new Vector2(offsetDistance, 0);   // 4 จากขวา
            case 4: return new Vector2(0, -offsetDistance);  // 5 จากล่าง
            case 5: return new Vector2(offsetDistance, 0);   // 6 จากขวา
            default: return new Vector2(0, offsetDistance);  // เผื่อไว้
        }
    }

    private void ShowNextPanel()
    {
        if (currentPanelIndex >= panels.Length)
        {
            // ถ้าโชว์ครบแล้ว แจ้งว่าพร้อมโหลด Scene ถัดไปเมื่อกดปุ่มอีกครั้ง
            isReadyToDismiss = true;
            return;
        }

        isAnimating = true;
        RectTransform currentPanel = panels[currentPanelIndex];
        currentPanel.gameObject.SetActive(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot("SFX_slide");
        }

        // ใช้ LeanTween เลื่อน panel กลับมาที่เดิม
        LeanTween.move(currentPanel, originalPositions[currentPanelIndex], 0.6f)
            .setEase(LeanTweenType.easeOutBack) // มีเด้งนิดๆ ตอนจบให้ดูสวยงาม
            .setOnComplete(() =>
            {
                isAnimating = false;
                currentPanelIndex++; // เมื่ออนิเมชันเสร็จ ถึงจะนับว่า Panel นี้โชว์เสร็จแล้ว
                
                // ถ้านี่คือ Panel สุดท้าย (โชว์ครบ 6 อันแล้ว) ก็ปรับสถานะเลย จะได้กดครั้งถัดไปแล้วเข้าเกม
                if (currentPanelIndex >= panels.Length)
                {
                    isReadyToDismiss = true;
                }
            });
    }

    void Update()
    {
        if (Input.anyKeyDown)
        {
            HandleInput();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // ถ้ากำลังอนิเมชันอยู่ ให้กดไม่ได้ชั่วคราว
        if (isAnimating) return;

        if (isReadyToDismiss)
        {
            Dismiss();
        }
        else
        {
            // โชว์ Panel ถัดไป
            ShowNextPanel();
        }
    }

    private void Dismiss()
    {
        isReadyToDismiss = false;
        gameObject.SetActive(true);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot("HUD_combo_3");
        }
        onCompleteCallback?.Invoke();
    }
}
