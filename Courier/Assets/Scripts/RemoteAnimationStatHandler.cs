using Newtonsoft.Json;
using UnityEngine;

public class RemoteAnimationStatHandler : ITcpMessageHandler
{
    private NetworkManager _networkManager;
    
    public RemoteAnimationStatHandler(NetworkManager networkManager)
    {
        _networkManager = networkManager;
    }
    public bool CanHandle(string json) => json.Contains("\"AnimationState\""); 
    public void Handle(string json)
    {
        var updates = JsonConvert.DeserializeObject<AnimationStateUpdate[]>(json);
        foreach (var update in updates)
        {
            HandleAnimationState(update);
        }
    }
    private void HandleAnimationState(AnimationStateUpdate update)
    {   
        if (_networkManager.RemotePlayers.ContainsKey(update.PlayerId))
        {
            Debug.Log($"[TCP] Обновление состояния анимации для игрока {update.PlayerId}: {update.AnimationState})");
            _networkManager.RemotePlayers[update.PlayerId].RemoteAnimation.SetAnimationState(update.AnimationState);
        }
    }
}