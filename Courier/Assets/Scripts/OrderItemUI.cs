using System;
using UnityEngine;
using UnityEngine.UI;

public class OrderItemUI : MonoBehaviour
{
    public Text descriptionText;
    public Text rewardText;
    public Text statusText;
    public Button takeButton;
    public Button pickupButton;
    public Button deliverButton;

    private Order _order;
    private bool _isFree;

    public Order Order => _order;
    public void Setup(Order order)
    {
        _order = order;
       

        descriptionText.text = order.Description;
        rewardText.text = order.Reward + "$";
        _isFree = String.IsNullOrEmpty(order.TakenByPlayerId);
        if (_isFree)
        {
            statusText.text = "Свободен";
            takeButton.gameObject.SetActive(true);
            pickupButton.gameObject.SetActive(false);
            deliverButton.gameObject.SetActive(false);

            takeButton.onClick.RemoveAllListeners();
            takeButton.onClick.AddListener(() => TakeOrder());
        }
        else
        {
            takeButton.gameObject.SetActive(false);

            if (!order.IsPickedUp)
            {
                statusText.text = "Ожидает получения";
                pickupButton.gameObject.SetActive(true);
                deliverButton.gameObject.SetActive(false);
                pickupButton.onClick.RemoveAllListeners();
                pickupButton.onClick.AddListener(() => PickupOrder());
            }
            else if (!order.IsCompleted)
            {
                statusText.text = "В пути";
                pickupButton.gameObject.SetActive(false);
                deliverButton.gameObject.SetActive(true);
                deliverButton.onClick.RemoveAllListeners();
                deliverButton.onClick.AddListener(() => DeliverOrder());
            }
        }
    }

    private void TakeOrder()
    {
        Debug.Log("TakeOrder for " + _order.Id);
        NetworkManager.Instance?.TakeOrder(_order.Id);
    }

    private void PickupOrder()
    {
        NetworkManager.Instance?.PickupOrder(_order.Id);
    }

    private void DeliverOrder()
    {
        NetworkManager.Instance?.DeliverOrder(_order.Id);
    }
}