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
           
        };
    }

    public void HandleMessage(string json)
    {
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