using System.Collections.Generic;
using UnityEngine;

public class TcpMessageHandler
{
    private NetworkManager _networkManager;
    private List<ITcpMessageHandler> _handlers;
    public TcpMessageHandler(NetworkManager networkManager)
    {
        _networkManager = networkManager;
        _handlers = new List<ITcpMessageHandler>
        {
            new NewPlayerMessageHandler(networkManager),
            new OrderMessageHandler(networkManager),
            new RemoteAnimationStatHandler(networkManager),
        };
    }

    public void HandleMessage(string json)
    {
        Debug.Log($"[TCP] Получено сообщение: '{json}'"); // ← добавь это!
        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(json))
            {
                handler.Handle(json);
                return;
            }
        }
        Debug.LogError($"[TCP] Неизвестный тип сообщения: {json}");
    }
}