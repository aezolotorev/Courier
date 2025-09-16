using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public int typeCharacter {get; set; }
    
    [SerializeField] private CharacterTypeController _characterTypeController;
    [SerializeField] private RemoteAnimation _remoteAnimation;
    public RemoteAnimation RemoteAnimation => _remoteAnimation;
    
    
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

        var speed = (_lastPosition - _currentPosition).magnitude;
        //_remoteAnimation.SetAnimationState(speed>0.005 ? "Run": "Idle") ;
        // Интерполяция поворота
        _currentRotationY = Mathf.LerpAngle(_currentRotationY, _targetRotationY, Time.deltaTime * _lerpSpeed);
        transform.rotation = Quaternion.Euler(0, _currentRotationY, 0);
    }

    public void UpdateState(float x, float y, float z, float yaw)
    {
        _targetPosition = new Vector3(x, y, z);
        _targetRotationY = yaw;
    }
}