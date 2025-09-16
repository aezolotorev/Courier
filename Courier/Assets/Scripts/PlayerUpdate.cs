using System;
using UnityEngine;

[Serializable]
public class PlayerPositionUpdate
{
    public string MessageType;
    public string PlayerId;
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
}

// Для создания нового игрока
[Serializable]
public class NewPlayerUpdate
{
    public string MessageType;
    public string PlayerId;
    public string Username;
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
    public int TypeCharacter;
}

[Serializable]
public class AnimationStateUpdate
{
    public string PlayerId;
    public string AnimationState;
}

[Serializable]
public class LoginResponse
{
    public string PlayerId;
    public int TypeCharacter;
}