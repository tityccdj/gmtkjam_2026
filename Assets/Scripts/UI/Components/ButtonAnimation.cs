using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class ButtonAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Animation Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    [Header("Keyboard / Gamepad Selection")]
    [SerializeField] private bool enableSelectionSlide = true;
    [SerializeField] private float selectionHorizontalOffset = 12f;

    [Header("Optional Rotation")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private float rotationAmount = 5f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector2 originalAnchoredPosition;
    private float originalRotationZ;
    private bool isPressed = false;
    private bool isPointerHovered = false;
    private bool isSelected = false;

    // Captured in Awake, not Start: buttons spawned from a template can be
    // deactivated before their first frame (see UICharacterSelect / UIStoryLevel
    // populating while Title.Start hides the panel), and OnDisable writes this
    // value straight back into the scale.
    void Awake()
    {
        rectTransform = transform as RectTransform;
        originalScale = transform.localScale;
        originalAnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
        originalRotationZ = transform.localEulerAngles.z;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CaptureCurrentPositionAsRestPosition();
        isPointerHovered = true;
        if (!isPressed)
        {
            AnimateState(highlighted: true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHovered = false;
        if (!isPressed)
        {
            AnimateState(isSelected);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CaptureCurrentPositionAsRestPosition();
        isPressed = true;
        AnimatePress();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFXOneShot("pop");
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        AnimateState(isPointerHovered || isSelected);
    }

    public void OnSelect(BaseEventData eventData)
    {
        CaptureCurrentPositionAsRestPosition();
        isSelected = true;
        if (!isPressed)
        {
            AnimateState(highlighted: true);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        if (!isPressed)
        {
            AnimateState(isPointerHovered);
        }
    }

    private void CaptureCurrentPositionAsRestPosition()
    {
        if (rectTransform != null && !isPointerHovered && !isSelected && !isPressed)
        {
            // Runtime-created buttons may be positioned by a LayoutGroup after
            // Awake, so capture the final layout position before animating.
            originalAnchoredPosition = rectTransform.anchoredPosition;
        }
    }

    private void AnimateState(bool highlighted)
    {
        LeanTween.cancel(gameObject);

        Vector3 targetScale = highlighted ? originalScale * hoverScale : originalScale;
        LeanTween.scale(gameObject, targetScale, animationDuration).setEase(easeType);

        if (rectTransform != null)
        {
            Vector2 targetPosition = originalAnchoredPosition;
            if (highlighted && enableSelectionSlide)
            {
                targetPosition += Vector2.right * selectionHorizontalOffset;
            }
            LeanTween.move(rectTransform, targetPosition, animationDuration).setEase(easeType);
        }

        if (enableRotation)
        {
            float targetRotation = highlighted
                ? originalRotationZ + rotationAmount
                : originalRotationZ;
            LeanTween.rotateZ(gameObject, targetRotation, animationDuration).setEase(easeType);
        }
    }

    private void AnimatePress()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * pressScale, animationDuration * 0.5f)
            .setEase(LeanTweenType.easeInOutQuad);
    }

    // Optional: Public method to play a bounce animation
    public void PlayBounceAnimation()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * 1.2f, 0.15f)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() =>
            {
                LeanTween.scale(gameObject, originalScale, 0.15f)
                    .setEase(LeanTweenType.easeInQuad);
            });
    }

    void OnDisable()
    {
        // Cancel all tweens when disabled to prevent errors
        LeanTween.cancel(gameObject);
        transform.localScale = originalScale;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, originalRotationZ);
        isPressed = false;
        isPointerHovered = false;
        isSelected = false;
    }
}
