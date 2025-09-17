using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject followTransform;
   
    public Vector2 _lookInput;
    

    public float rotationPower = 3f;

    public float maxVerticalAngle;
    public float minVerticalAngle;

    
    private InputSystem_Actions _controls;
    private void Start()
    {
        _controls = InputManager.Instance?.Controls;
        if (_controls != null)
        {

            _controls.Player.Look.performed += ctx => _lookInput = ctx.ReadValue<Vector2>();
            _controls.Player.Look.canceled += ctx => _lookInput = Vector2.zero;

        }
    }

    private void Update()
    {
        followTransform.transform.rotation *= Quaternion.AngleAxis(_lookInput.x * rotationPower, Vector3.up);
       
        followTransform.transform.rotation *= Quaternion.AngleAxis(-_lookInput.y * rotationPower, Vector3.right);

        var angles = followTransform.transform.localEulerAngles;
        angles.z = 0;

        var angle = followTransform.transform.localEulerAngles.x;

       
        if (angle > 180 && angle < minVerticalAngle)
        {
            angles.x = minVerticalAngle;
        }
        else if(angle < 180 && angle > maxVerticalAngle)
        {
            angles.x = maxVerticalAngle;
        }


        followTransform.transform.localEulerAngles = angles;
       
        followTransform.transform.localEulerAngles = new Vector3(angles.x, angles.y, 0);
    }
}