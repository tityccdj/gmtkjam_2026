using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class ButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Animation Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    [Header("Optional Rotation")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private float rotationAmount = 5f;

    private Vector3 originalScale;
    private bool isPressed = false;

    void Start()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isPressed)
        {
            // Scale up animation
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale * hoverScale, animationDuration)
                .setEase(easeType);

            // Optional rotation
            if (enableRotation)
            {
                LeanTween.rotateZ(gameObject, rotationAmount, animationDuration)
                    .setEase(easeType);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isPressed)
        {
            // Scale back to normal
            LeanTween.cancel(gameObject);
            LeanTween.scale(gameObject, originalScale, animationDuration)
                .setEase(easeType);

            // Reset rotation
            if (enableRotation)
            {
                LeanTween.rotateZ(gameObject, 0f, animationDuration)
                    .setEase(easeType);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        // Scale down for press effect
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * pressScale, animationDuration * 0.5f)
            .setEase(LeanTweenType.easeInOutQuad);

        AudioManager.Instance.PlaySFXOneShot("pop");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        // Scale back to hover size if still hovering, otherwise normal
        LeanTween.cancel(gameObject);
        Vector3 targetScale = eventData.hovered.Contains(gameObject) ? originalScale * hoverScale : originalScale;
        LeanTween.scale(gameObject, targetScale, animationDuration * 0.5f)
            .setEase(easeType);
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
    }
}
