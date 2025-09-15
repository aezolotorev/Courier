using Newtonsoft.Json;

public class OrderMessageHandler : ITcpMessageHandler
{
    private NetworkManager _networkManager;

    public OrderMessageHandler(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public bool CanHandle(string json) => json.Contains("\"Order\"");

    public void Handle(string json)
    {
        var updates = JsonConvert.DeserializeObject<OrderUpdate[]>(json);
        foreach (var update in updates)
        {
            UIManager.Instance.HandleOrderUpdate(update);
        }
    }
}