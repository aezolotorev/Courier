using Unity.Cinemachine;
using UnityEngine;

public class CameraManager: MonoBehaviour
{
    [SerializeField] private StickToObject _stickToObject;
    public StickToObject StickToObject => _stickToObject;
    public void SetTrackingTarget(Transform target)
    {
        _stickToObject.target = target;
    }
}