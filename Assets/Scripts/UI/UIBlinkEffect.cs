using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UIBlinkEffect : MonoBehaviour
{
    public float blinkSpeed = 2f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 1f;

    private SpriteRenderer targetGraphic;

    private void Awake()
    {
        targetGraphic = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (IsEnterPressed())
        {
            gameObject.SetActive(false);
            return;
        }

        if (targetGraphic != null)
        {
            Color c = targetGraphic.color;
            // Use Sin to smoothly oscillate between 0 and 1, then lerp between min and max alpha
            float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI) + 1f) * 0.5f;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            targetGraphic.color = c;
        }
    }

    private bool IsEnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }
}
