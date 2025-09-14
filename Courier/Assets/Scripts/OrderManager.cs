using System.Collections.Generic;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;

    public GameObject orderMarkerPrefab;

    private Dictionary<string, GameObject> _orderMarkers = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void HandleOrderUpdate(OrderUpdate update)
    {
        var order = update.Order;

        if (order.IsCompleted)
        {
            // ... удаляем маркер
            return;
        }

        if (order.IsPickedUp)
        {
            // ✅ Заказ подобран — можно везти к точке доставки
            if (_orderMarkers.ContainsKey(order.Id))
            {
                Destroy(_orderMarkers[order.Id]);
                _orderMarkers.Remove(order.Id);
            }
            return;
        }

        if (string.IsNullOrEmpty(order.TakenByPlayerId))
        {
            // ✅ Свободный заказ — показываем маркер
            if (!_orderMarkers.ContainsKey(order.Id))
            {
                var marker = Instantiate(orderMarkerPrefab, new Vector3(order.PickupX, order.PickupY + 1, order.PickupZ), Quaternion.identity);
                marker.GetComponent<OrderMarker>().OrderId = order.Id;
                marker.GetComponent<OrderMarker>().Order = order;
                _orderMarkers[order.Id] = marker;
            }
        }
        else
        {
            // ✅ Заказ взят в выполнение, но не подобран — маркер скрыт
            if (_orderMarkers.ContainsKey(order.Id))
            {
                _orderMarkers[order.Id].SetActive(false);
            }
        }
    }
}

