using UnityEngine;

public class RemoteAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private string currentAnimState = "";
    
    public void SetAnimationState(string animState)
    {
        Debug.Log("SetAnimationState: " + animState);
        if (animState != currentAnimState)
        {
            currentAnimState = animState;
            // ✅ Плавный переход к новому состоянию
            animator.CrossFadeInFixedTime(animState, 0.15f);
        }
    }

    // Для событий (подбор, доставка) — используем Trigger
    public void TriggerAnimation(string triggerName)
    {
        animator.SetTrigger(triggerName);
    }
}