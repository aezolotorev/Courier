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
        var updates = JsonConvert.DeserializeObject<NewPlayerUpdate[]>(json);
        foreach (var update in updates)
        {
            HandleNewPlayer(update);
        }
    }
    private void HandleNewPlayer(NewPlayerUpdate newPlayer)
    {
        if (!_networkManager.RemotePlayers.ContainsKey(newPlayer.PlayerId))
        {
            var playerPrefab = Resources.Load<RemotePlayer>("RemotePlayer");
            if (playerPrefab != null)
            {
                var playerRemote = GameObject.Instantiate(playerPrefab, new Vector3(newPlayer.X, newPlayer.Y, newPlayer.Z), Quaternion.identity);
                playerRemote.gameObject.name = "Player_" + newPlayer.Username;
              
                if (playerRemote != null)
                {
                    playerRemote.typeCharacter = newPlayer.TypeCharacter;
                    playerRemote.PlayerId = newPlayer.PlayerId;
                    playerRemote.Username = newPlayer.Username;
                    playerRemote.typeCharacter = newPlayer.TypeCharacter;
                }
                playerRemote.SetUpCharacter();
                _networkManager.RemotePlayers[newPlayer.PlayerId] = playerRemote;
                playerRemote?.UpdateState(newPlayer.X, newPlayer.Y, newPlayer.Z, newPlayer.Yaw, newPlayer.MoveX, newPlayer.MoveZ);  
                _networkManager.newPlayerConnected?.Invoke(playerRemote?.Username);
            }
        }
    }
}