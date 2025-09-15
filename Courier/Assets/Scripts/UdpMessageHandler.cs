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
        if (json.Contains("\"PlayerId\"") && json.Contains("\"Yaw\""))
        {
            var playerUpdate = JsonConvert.DeserializeObject<PlayerUpdate>(json);
            if (playerUpdate != null && playerUpdate.PlayerId != _networkManager.PlayerId)
            {
                HandleRemotePlayerUpdate(playerUpdate);
            }
        }
    }
    
    private void HandleRemotePlayerUpdate(PlayerUpdate update)
    {
        if (!_networkManager.RemotePlayers.TryGetValue(update.PlayerId, out var playerRemote))
        {
            var playerPrefab = Resources.Load<RemotePlayer>("RemotePlayer");
            if (playerPrefab != null)
            {
                playerRemote = GameObject.Instantiate(playerPrefab, new Vector3(update.X, update.Y, update.Z), Quaternion.identity);
                playerRemote.gameObject.name = "Player_" + update.Username;
                
                if (playerRemote != null)
                {
                    playerRemote.PlayerId = update.PlayerId;
                    playerRemote.Username = update.Username;
                }
                _networkManager.RemotePlayers[update.PlayerId] = playerRemote;
            }
        }

        playerRemote?.UpdateState(update.X, update.Y, update.Z, update.Yaw);
    }
}