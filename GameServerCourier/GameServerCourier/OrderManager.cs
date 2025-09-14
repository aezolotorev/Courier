using System.Collections.Concurrent;

public class OrderManager
{
    private static readonly ConcurrentDictionary<string, Order> ActiveOrders = new();
    private static readonly ConcurrentDictionary<string, Order> PreviousStates = new();
    private static readonly Random Rnd = new();

    // Генерируем случайный заказ
    public static Order GenerateOrder()
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            Description = GetRandomDescription(),
            PickupX = Rnd.Next(-10, 10),
            PickupY = 0,
            PickupZ = Rnd.Next(-10, 10),
            DropoffX = Rnd.Next(-10, 10),
            DropoffY = 0,
            DropoffZ = Rnd.Next(-10, 10),
            Reward = Rnd.Next(50, 200)
        };

        ActiveOrders[order.Id] = order;
        PreviousStates[order.Id] = new Order { /* копируем начальное состояние */ }; // ← важно!

        return order;
    }
    
    public static bool UpdateOrder(string id, Action<Order> updateAction)
    {
        if (ActiveOrders.TryGetValue(id, out var order))
        {
            var previous = PreviousStates[id];
            var before = new Order
            {
                TakenByPlayerId = previous.TakenByPlayerId,
                IsPickedUp = previous.IsPickedUp,
                IsCompleted = previous.IsCompleted
            };

            updateAction(order);

            var hasChanged = order.HasStateChanged(before);
            if (hasChanged)
            {
                // Обновляем предыдущее состояние
                PreviousStates[id] = new Order
                {
                    TakenByPlayerId = order.TakenByPlayerId,
                    IsPickedUp = order.IsPickedUp,
                    IsCompleted = order.IsCompleted
                };
            }

            return hasChanged;
        }
        return false;
    }

    private static string GetRandomDescription()
    {
        var descriptions = new[] { "Пицца", "Посылка", "Цветы", "Еда", "Документы", "Техника" };
        return descriptions[Rnd.Next(descriptions.Length)];
    }

    public static Order? GetOrder(string id) => ActiveOrders.GetValueOrDefault(id);
    public static Order[] GetAllActiveOrders() => ActiveOrders.Values.Where(o => !o.IsCompleted).ToArray();
    public static void CompleteOrder(string id)
    {
        if (ActiveOrders.TryRemove(id, out var order))
        {
            order.IsCompleted = true;
        }
    }
}


