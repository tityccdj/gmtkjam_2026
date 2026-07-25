using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CharacterAnim : MonoBehaviour
{
    [SerializeField] private string idleAnimationName = "woman";
    [SerializeField] private string attackAnimationName = "woman_attack";

    private const float DefaultAttackDuration = 0.5f;
    private const float PulseDuration = 0.28f;
    private const float PulseStartScale = 0.2f;
    private const float PulseEndScale = 2.2f;

    private Animator animator;
    private Sprite pulseSprite;
    private Coroutine attackRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        pulseSprite = LoadPulseSprite();
        PlayAnimation(idleAnimationName);
    }

    public void PlayAttack()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        attackRoutine = StartCoroutine(PlayAttackRoutine());
    }

    private IEnumerator PlayAttackRoutine()
    {
        PlayAnimation(attackAnimationName);
        StartCoroutine(PlayAttackPulse());

        yield return new WaitForSeconds(GetAnimationDuration(attackAnimationName));

        PlayAnimation(idleAnimationName);
        attackRoutine = null;
    }

    private IEnumerator PlayAttackPulse()
    {
        if (pulseSprite == null)
        {
            yield break;
        }

        GameObject pulseObject = new GameObject("AttackPulse");
        pulseObject.transform.SetParent(transform, false);
        pulseObject.transform.localPosition = Vector3.zero;
        pulseObject.transform.localScale = Vector3.one * PulseStartScale;

        SpriteRenderer pulseRenderer = pulseObject.AddComponent<SpriteRenderer>();
        pulseRenderer.sprite = pulseSprite;
        pulseRenderer.color = new Color(1f, 0.35f, 0.25f, 0.65f);

        SpriteRenderer characterRenderer = GetComponentInChildren<SpriteRenderer>();
        if (characterRenderer != null)
        {
            pulseRenderer.sortingLayerID = characterRenderer.sortingLayerID;
            pulseRenderer.sortingOrder = characterRenderer.sortingOrder + 10;
        }

        float elapsed = 0f;
        while (elapsed < PulseDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / PulseDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            pulseObject.transform.localScale = Vector3.one *
                Mathf.Lerp(PulseStartScale, PulseEndScale, easedProgress);

            Color color = pulseRenderer.color;
            color.a = Mathf.Lerp(0.65f, 0f, progress);
            pulseRenderer.color = color;
            yield return null;
        }

        Destroy(pulseObject);
    }

    private void PlayAnimation(string animationName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(animationName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning(
                $"Animation state '{animationName}' was not found on {name}.",
                this);
            return;
        }

        animator.Play(stateHash, 0, 0f);
    }

    private float GetAnimationDuration(string animationName)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == animationName)
                {
                    return Mathf.Max(clip.length, 0.01f);
                }
            }
        }

        return DefaultAttackDuration;
    }

    private static Sprite LoadPulseSprite()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("sprites/circle");
        return sprites.Length > 0 ? sprites[0] : null;
    }
}
