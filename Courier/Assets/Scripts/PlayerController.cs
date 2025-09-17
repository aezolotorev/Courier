using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("=== References ===")]
     private Camera _mainCamera;
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private CharacterTypeController _characterTypeController;
    [SerializeField] private CharacterAnimationCointroller _characterAnimationController;
    [SerializeField] private Transform cameraLook;
    public Transform CameraLook => cameraLook;
    [Header("=== Movement ===")]
    public float moveSpeed = 5f;
    public float sprintSpeedMultiplier = 2f;
    public float rotationLerp = 25f; // плавность поворота в направлении движения

    [Header("=== Stamina ===")]
    public float maxStamina = 100f;
    public float sprintStaminaDrainRate = 20f; // в секунду
    public float staminaRegenRate = 15f;       // в секунду

    [Header("=== Networking ===")]
    public float sendInterval = 0.05f; // 20 раз в секунду

    // Input
    private InputSystem_Actions _controls;
    private Vector2 _moveInput;

    // State
    private float _currentStamina;
    private bool _isSprinting;
    private string _lastAnimState = "Idle";

    // Networking
    private Vector3 _lastSentPosition = Vector3.zero;
    private Vector2 _lastMoveVector = Vector2.zero;
    private float _lastYaw = 0f;
    private float _lastSendTime = 0f;

    // Cached
    private Transform _playerTransform;
    
    private void Start()
    {
        _playerTransform = transform;
        
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[PlayerController] Основная камера не найдена!");
            }
        }
        
        _controls = InputManager.Instance?.Controls;
        if (_controls != null)
        {
            _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
            _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
           
            _controls.Player.Sprint.performed += ctx => { }; // только IsPressed
            _controls.Player.Sprint.canceled += ctx => { };

            _controls.Player.Interact.performed += ctx => TryTakeOrder();
            _controls.Player.DeliverOrder.performed += ctx => TryDeliverOrder();
            _controls.Player.PickUpOrder.performed += ctx => TryPickupOrder();

            _controls.Enable();
        }
        
        _currentStamina = maxStamina;
    }

    public void SetTrasformFormCameraController(GameObject followObject)
    {
        _cameraController.followTransform = followObject;
    }

    private void OnEnable()
    {
        _controls?.Enable();
    }

    private void OnDisable()
    {
        if (_controls != null)
        {
            _controls.Player.Move.performed -= ctx => _moveInput = ctx.ReadValue<Vector2>();
            _controls.Player.Move.canceled -= ctx => _moveInput = Vector2.zero;

            _controls.Player.Sprint.performed -= ctx => { }; // только IsPressed
            _controls.Player.Sprint.canceled -= ctx => { };
            
            _controls.Player.Interact.performed -= ctx => TryTakeOrder();
            _controls.Player.DeliverOrder.performed -= ctx => TryDeliverOrder();
            _controls.Player.PickUpOrder.performed -= ctx => TryPickupOrder();
        }
    }

    public void SetCharacterType(int typeCharacter)
    {
        _characterTypeController.SetCharacterType(typeCharacter);
    }

    private void Update()
    {
        HandleStamina();
        HandleMovement();
        HandleAnimationState();
        UpdateLocalAnimation();
        SendNetworkUpdates();
    }

    private void HandleStamina()
    {
        // Спринт активен, если зажат Sprint, есть движение и стамина > 0
        _isSprinting = _controls.Player.Sprint.IsPressed() && _moveInput.magnitude > 0.1f && _currentStamina > 0;

        if (_isSprinting)
        {
            _currentStamina -= sprintStaminaDrainRate * Time.deltaTime;
            _currentStamina = Mathf.Max(0, _currentStamina);
        }
        else if (_currentStamina < maxStamina && _moveInput.magnitude < 0.1f) // ← только если стоит
        {
            _currentStamina += staminaRegenRate * Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        if (_mainCamera == null) return;
       
        Vector3 forward = _mainCamera.transform.forward;
        Vector3 right = _mainCamera.transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;
        moveDirection.Normalize();
       
        float speedMultiplier = _isSprinting ? sprintSpeedMultiplier : 1f;
        Vector3 move = moveDirection * moveSpeed * speedMultiplier * Time.deltaTime;
       
        transform.Translate(move, Space.World);
        
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp * Time.deltaTime);
        }
    }
    
    private float _currentSpeed = 0f;
    private float _speedVelocity = 0f; // для SmoothDamp

    private void UpdateLocalAnimation()
    {
        float targetSpeed = 0f;
        if (_moveInput.magnitude > 0.1f)
        {
            targetSpeed = _isSprinting ? 2f : 1f;
        }

        // ✅ SmoothDamp — плавное замедление/ускорение
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, 0.1f);

        _characterAnimationController.SetMovementSpeed(_currentSpeed);
    }
    
    /*private void UpdateLocalAnimation()
    {
        float speedValue = 0f;

        if (_moveInput.magnitude > 0.1f)
        {
            speedValue = _isSprinting ? 2f : 1f;
        }

        _characterAnimationController.SetMovementSpeed(speedValue);
    }*/

    private void HandleAnimationState()
    {
        float speed = _moveInput.magnitude;
        string animState;

        if (speed < 0.1f)
        {
            animState = "Idle";
        }
        else if (_isSprinting)
        {
            animState = "Sprint";
        }
        else
        {
            animState = "Run";
        }

        if (animState != _lastAnimState)
        {
            _lastAnimState = animState;
            NetworkManager.Instance?.SendAnimationState(animState);
        }
    }

    private void SendNetworkUpdates()
    {
        if (Time.time - _lastSendTime < sendInterval) return;

        Vector3 pos = _playerTransform.position;
        float yaw = _playerTransform.eulerAngles.y;

        // ✅ Отправляем направление движения (нормализованное)
        float moveX = _moveInput.x;
        float moveZ = _moveInput.y;
        Vector2 moveVector = new Vector2(moveX, moveZ);

        // ✅ Отправляем только при изменении
        if ((pos - _lastSentPosition).sqrMagnitude > 0.01f || 
            moveVector != _lastMoveVector || 
            Mathf.Abs(yaw - _lastYaw) > 0.1f)
        {
            _lastSentPosition = pos;
            _lastMoveVector = moveVector;
            _lastYaw = yaw;
            _lastSendTime = Time.time;

            var movementUpdate = new PlayerPositionUpdate
            {
                MessageType = "PlayerUpdate",
                PlayerId = NetworkManager.Instance.PlayerId,
                X = pos.x,
                Y = pos.y,
                Z = pos.z,
                MoveX = moveX,   // -1..1 (влево/вправо относительно камеры)
                MoveZ = moveZ,   // -1..1 (назад/вперёд относительно камеры)
                Yaw = yaw        // абсолютный поворот игрока
            };

            NetworkManager.Instance?.SendMovementUpdate(movementUpdate);
        }
    }

    // ======================
    // Команды (заглушки — реализуй по своему)
    // ======================

    private void TryTakeOrder()
    {
        // Логика взятия заказа
        Debug.Log("TryTakeOrder");
    }

    private void TryPickupOrder()
    {
        // Логика подбора заказа
        Debug.Log("TryPickupOrder");
    }

    private void TryDeliverOrder()
    {
        // Логика доставки заказа
        Debug.Log("TryDeliverOrder");
    }
}