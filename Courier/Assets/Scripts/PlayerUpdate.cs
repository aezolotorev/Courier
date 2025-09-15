using System;

[Serializable]
public class PlayerUpdate
{
    public string MessageType;
    public string PlayerId;
    public string Username; 
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
}