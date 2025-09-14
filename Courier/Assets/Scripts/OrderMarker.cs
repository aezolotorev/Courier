using UnityEngine;
using UnityEngine.UI;


public class OrderMarker : MonoBehaviour
{
    public string OrderId { get; set; }
    public Order Order { get; set; }

    public Text descriptionText;
    public Text rewardText;

    private void Start()
    {
        if (descriptionText != null) descriptionText.text = Order.Description;
        if (rewardText != null) rewardText.text = Order.Reward + "$";
    }

    private void Update()
    {
        // Вращаем иконку
        transform.Rotate(0, 45 * Time.deltaTime, 0);
    }

    private void OnMouseDown()
    {
        // Берём заказ при клике
        NetworkManager.Instance.TakeOrder(OrderId);
        gameObject.SetActive(false);
    }
}