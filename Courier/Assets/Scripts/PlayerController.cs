using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private CharacterTypeController _characterTypeController;
    private int _typeCharacter;
    [SerializeField] private Animator animator;
    public float moveSpeed = 5f;
    public float lookSensitivity = 2f;
    private string _lastAnimState = "";
    
    private InputSystem_Actions _controls;
    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private float lastSendTime = 0f;
    public float sendInterval = 0.05f;
    private void Start() 
    {
        _controls = InputManager.Instance?.Controls;

        // Подписываемся на события
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        _controls.Player.Look.performed += ctx => _lookInput = ctx.ReadValue<Vector2>();
        _controls.Player.Look.canceled += ctx => _lookInput = Vector2.zero;

        _controls.Player.Interact.performed += ctx => TryTakeOrder();
        _controls.Player.DeliverOrder.performed += ctx => TryDeliverOrder();
        _controls.Player.PickUpOrder.performed += ctx => TryPickupOrder();
    }

    public void SetCharacterType(int typeCharacter)
    {
        _typeCharacter = typeCharacter;
        _characterTypeController.SetCharacterType(_typeCharacter);
    }

    private void OnEnable()
    { 
        _controls?.Enable();
    }

    private void OnDisable()
    {
        _controls.Player.Move.performed -= ctx => _moveInput = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled -= ctx => _moveInput = Vector2.zero;

        _controls.Player.Look.performed -= ctx => _lookInput = ctx.ReadValue<Vector2>();
        _controls.Player.Look.canceled -= ctx => _lookInput = Vector2.zero;

        _controls.Player.Interact.performed -= ctx => TryTakeOrder();
        _controls.Player.DeliverOrder.performed -= ctx => TryDeliverOrder();
        _controls.Player.PickUpOrder.performed -= ctx => TryPickupOrder();
    }

    private void Update()
    {
        // Движение
        Vector3 move = new Vector3(_moveInput.x, 0, _moveInput.y) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.Self);

        float speed = move.magnitude;
        
        animator.SetFloat("Speed", speed);
        
        // Поворот (по мыши)
        float mouseX = _lookInput.x * lookSensitivity;
        transform.Rotate(0, mouseX, 0);

        if (Time.time - lastSendTime >= sendInterval)
        {
            lastSendTime = Time.time;
            Debug.Log("Отправляем позицию и поворот");
            NetworkManager.Instance.SendPositionAndRotation(transform.position, transform.eulerAngles.y);
        }
        string animState = speed >= 0.01 ? "Run" : "Idle";
        if (animState != _lastAnimState)
        {
            _lastAnimState = animState;
            NetworkManager.Instance.SendAnimationState(animState); // → TCP
        }
    }
    
    
    private void TryPickupOrder()
    {
        var uiManager = UIManager.Instance;
       /*if (uiManager?.CurrentOrder != null && !uiManager.CurrentOrder.IsPickedUp)
        {
            float distance = Vector3.Distance(transform.position, new Vector3(uiManager.CurrentOrder.PickupX, uiManager.CurrentOrder.PickupY, uiManager.CurrentOrder.PickupZ));
            if (distance <= 3f)
            {
                // ✅ Физически подбираем заказ
                NetworkManager.Instance?.PickupOrder(uiManager.CurrentOrder.Id);
            }
            else
            {
                Debug.Log("Слишком далеко для подбора!");
            }
        }*/
    }

    private void TryTakeOrder()
    {
        // Луч к ближайшему заказу
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var orderMarker = hit.collider.GetComponent<OrderMarker>();
            if (orderMarker != null)
            {
                NetworkManager.Instance?.TakeOrder(orderMarker.OrderId);
            }
        }
    }

    private void TryDeliverOrder()
    {
        // Проверяем, есть ли активный заказ
        var uiManager = UIManager.Instance;
        /*if (uiManager != null && uiManager.CurrentOrder != null)
        {
            NetworkManager.Instance?.DeliverOrder(uiManager.CurrentOrder.Id);
        }*/
    }
    
    private void OnDestroy()
    {
        if (_controls != null)
        {
            _controls.Dispose(); // ← освобождает ресурсы
        }
    }
}