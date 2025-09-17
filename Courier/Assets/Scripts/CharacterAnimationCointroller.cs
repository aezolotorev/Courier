using UnityEngine;

public class CharacterAnimationCointroller : MonoBehaviour
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    [SerializeField] private Animator _animator;

    // Публичные методы — для PlayerController и других систем
    public void SetMovementSpeed(float speed)
    {
        _animator.SetFloat(Speed, speed);
    }

    public void TriggerAction(string actionName)
    {
        _animator.SetTrigger(actionName);
    }

    // Для событий
    public void TriggerAnimation(string triggerName)
    {
        _animator.SetTrigger(triggerName);
    }
}