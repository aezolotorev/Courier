using Newtonsoft.Json;
using UnityEngine;

public class UdpMessageHandler
{
    private NetworkManager _networkManager;

    public UdpMessageHandler(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public void HandleMessage(string json)
    {
        if (json.Contains("\"MessageType\":\"PlayerUpdate\""))
        {
            var playerUpdate = JsonConvert.DeserializeObject<PlayerPositionUpdate>(json);
            if (playerUpdate != null && playerUpdate.PlayerId != _networkManager.PlayerId)
            {
                HandleRemotePlayerUpdate(playerUpdate);
            }
        }
    }
    
    private void HandleRemotePlayerUpdate(PlayerPositionUpdate update)
    {
        if (_networkManager.RemotePlayers.TryGetValue(update.PlayerId, out var playerRemote))
        {
            playerRemote?.UpdateState(update.X, update.Y, update.Z, update.Yaw, update.MoveX, update.MoveZ);
        }
    }
}