using UnityEngine;

public class RemoteCharacterAnimationController : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    private string _currentAnimState = "";
    
    public void SetAnimationState(string animState)
    {
        if (animState != _currentAnimState)
        {
            _currentAnimState = animState;
            _animator.CrossFadeInFixedTime(animState, 0.25f);
        }
    }
  
    public void TriggerAnimation(string triggerName)
    {
        _animator.SetTrigger(triggerName);
    }
}