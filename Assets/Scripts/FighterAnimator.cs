using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FighterAnimator : MonoBehaviour
{
    private Animator anim;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public void TriggerAttack()
    {
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }
    }
}
