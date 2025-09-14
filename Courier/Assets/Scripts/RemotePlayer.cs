using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    public string PlayerId { get; set; }
    public string Username { get; set; }

    private Vector3 _targetPosition;
    private float _targetRotationY;
    private Vector3 _currentPosition;
    private float _currentRotationY;
    private float _lerpSpeed = 10f;

    private void Start()
    {
        _currentPosition = transform.position;
        _currentRotationY = transform.eulerAngles.y;
    }

    private void Update()
    {
        // Интерполяция позиции
        _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, Time.deltaTime * _lerpSpeed);
        transform.position = _currentPosition;

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