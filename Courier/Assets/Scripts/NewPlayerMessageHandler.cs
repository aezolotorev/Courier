using Newtonsoft.Json;
using UnityEngine;

public class NewPlayerMessageHandler : ITcpMessageHandler
{
    private NetworkManager _networkManager;

    public NewPlayerMessageHandler(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }

    public bool CanHandle(string json) => json.Contains("\"MessageType\":\"NewPlayer\"");

    public void Handle(string json)
    {
        var updates = JsonConvert.DeserializeObject<PlayerUpdate[]>(json);
        foreach (var update in updates)
        {
            HandleNewPlayer(update);
        }
    }
    private void HandleNewPlayer(PlayerUpdate update)
    {
        if (!_networkManager.RemotePlayers.ContainsKey(update.PlayerId))
        {
            var playerPrefab = Resources.Load<RemotePlayer>("RemotePlayer");
            if (playerPrefab != null)
            {
                var playerRemote = GameObject.Instantiate(playerPrefab, new Vector3(update.X, update.Y, update.Z), Quaternion.identity);
                playerRemote.gameObject.name = "Player_" + update.Username;
              
                if (playerRemote != null)
                {
                    playerRemote.PlayerId = update.PlayerId;
                    playerRemote.Username = update.Username;
                }
                _networkManager.RemotePlayers[update.PlayerId] = playerRemote;
                playerRemote?.UpdateState(update.X, update.Y, update.Z, update.Yaw);
                _networkManager.newPlayerConnected?.Invoke(playerRemote?.Username);
            }
        }
    }
}