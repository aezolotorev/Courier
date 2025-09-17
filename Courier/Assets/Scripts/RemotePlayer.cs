using UnityEngine;
using UnityEngine.Serialization;

public class RemotePlayer : MonoBehaviour
{
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public int typeCharacter {get; set; }
    
    [SerializeField] private CharacterTypeController _characterTypeController;
    [SerializeField] private RemoteCharacterAnimationController characterAnimationCointroller;
    public RemoteCharacterAnimationController RemoteCharacterAnimationCointroller => characterAnimationCointroller;
    
    
    private Vector3 _targetPosition;
    private float _targetRotationY;
    private Vector3 _currentPosition;
    private float _currentRotationY;
    private float _lerpSpeed = 10f;
    private Vector3 _lastPosition;
    
    public void  SetUpCharacter()
    {
        _characterTypeController.SetCharacterType(typeCharacter);
    }
    
    private void Start()
    {
        _lastPosition = transform.position;
        _currentPosition = transform.position;
        _currentRotationY = transform.eulerAngles.y;
    }

    private void Update()
    {
        _lastPosition = transform.position;
        _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, Time.deltaTime * _lerpSpeed);
        transform.position = _currentPosition;
        
        // Интерполяция поворота
        _currentRotationY = Mathf.LerpAngle(_currentRotationY, _targetRotationY, Time.deltaTime * _lerpSpeed);
        transform.rotation = Quaternion.Euler(0, _currentRotationY, 0);
    }

    public void UpdateState(float x, float y, float z, float yaw, float MoveX, float MoveZ)
    {
        _targetPosition = new Vector3(x, y, z);
        _targetRotationY = yaw;
        //characterAnimationCointroller.SetMoveAnimation(MoveX, MoveZ); ;
    }
}