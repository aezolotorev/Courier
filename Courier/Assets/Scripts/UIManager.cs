using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI; 
using System.Linq;
using UnityEngine.Serialization;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // UI References
    public GameObject phonePanel; // Панель "телефона"
    public GameObject notificationPanel; // Панель уведомлений
    public Text notificationBadge; // Маячок уведомлений (текст с числом)
    
    public Transform ordersContainer; // Контейнер для списка свободных заказов
    
    public OrderItemUI orderItemPrefab; // Префаб элемента списка заказа (с OrderItemUI.cs)

  
    private InputSystem_Actions _controls;

    private Dictionary<string, OrderItemUI>  _ordersItems = new ();

    private bool isOpen=false;
    private int newOrdersCount = 0;
    
    [SerializeField] private Button myOrdersButton;
    [SerializeField] private Button allOrdersButton;
    [SerializeField] private Button freeOrdersButton;
    public enum TypeList
    {
        All,
        My, 
        Free
    }
    
    public TypeList CurrentList = TypeList.All;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
 

    private void OnEnable() => _controls?.Enable();
    private void OnDisable() => _controls?.Disable();
    private void OnDestroy()
    {
        foreach (var item in _ordersItems)
        {
            if(item.Value.gameObject == null) continue;
            Destroy(item.Value.gameObject);
        }
        _ordersItems.Clear();
    }

    private void Start()
    {
        _controls = InputManager.Instance.Controls;
        _controls.Player.OpenPhone.performed += ctx => TogglePhone();
        myOrdersButton.onClick.AddListener(() => { CurrentList = TypeList.My; UpdateCurrentList(); });
        freeOrdersButton.onClick.AddListener(() => { CurrentList = TypeList.Free; UpdateCurrentList(); });
        allOrdersButton.onClick.AddListener(() => { CurrentList = TypeList.All; UpdateCurrentList(); });
        if (phonePanel != null) phonePanel.SetActive(false);
        UpdateNotificationBadge();
        isOpen = phonePanel.activeSelf;
    }

    private void UpdateCurrentList()
    {
        foreach (var item in _ordersItems)
        {
            item.Value.gameObject.SetActive(true);
            
            if (CurrentList == TypeList.My)
            {
                if (item.Value.Order.TakenByPlayerId != NetworkManager.Instance?.PlayerId)
                    item.Value.gameObject.SetActive(false);
            }
            else if (CurrentList == TypeList.Free)
            {
                if (item.Value.Order.TakenByPlayerId == NetworkManager.Instance?.PlayerId)
                    item.Value.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Переключает видимость панели телефона
    /// </summary>
    private void TogglePhone()
    {
        if (phonePanel != null)
        {
            isOpen = !isOpen;
            phonePanel.SetActive(isOpen);
            newOrdersCount = 0;
            UpdateNotificationBadge();
        }
    }

    /// <summary>
    /// Обрабатывает обновление заказа от сервера
    /// </summary>
    ///
   
    public void HandleOrderUpdate(OrderUpdate update)
    {
        if (update?.Order == null) return;
        
        var order = update.Order;

        if (update.Type == "NEW")
        {
            newOrdersCount++;
            OrderItemUI itm = Instantiate(orderItemPrefab, ordersContainer);
            itm.Setup(order);
            _ordersItems.Add(order.Id, itm);
        }

        if (update.Type == "UPDATE")
        {
            if (_ordersItems.ContainsKey(order.Id))
            {
                if (!string.IsNullOrEmpty(order.TakenByPlayerId) && order.TakenByPlayerId != NetworkManager.Instance?.PlayerId)
                {
                    Destroy(_ordersItems[order.Id].gameObject);
                    _ordersItems.Remove(order.Id);
                }
                else
                {
                    _ordersItems[order.Id].Setup(order);
                }
            }
            else if(!_ordersItems.ContainsKey(order.Id)&& string.IsNullOrEmpty(order.TakenByPlayerId))
            {
                newOrdersCount++;
                OrderItemUI itm = Instantiate(orderItemPrefab, ordersContainer);
                itm.Setup(order);
                _ordersItems.Add(order.Id, itm);
            }
        }
        
        if(!isOpen)
           UpdateNotificationBadge();
    }


    private void UpdateNotificationBadge()
    {
        if (isOpen)
        {
            notificationPanel.gameObject.SetActive(false);
            return;
        }


        int count = newOrdersCount;
        notificationBadge.text = count > 0 ? count.ToString() : "";
        notificationPanel.gameObject.SetActive(count > 0);
    }
}

