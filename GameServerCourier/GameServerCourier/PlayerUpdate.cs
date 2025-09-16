public class PlayerPositionUpdate
{
    public string MessageType { get; set; } = "PlayerUpdate";
    public string PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
}

public class NewPlayerUpdate
{
    public string MessageType { get; set; } = "NewPlayer";
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public int TypeCharacter { get; set; } // ← только здесь!
}

[Serializable]
public class LoginResponse
{
    public string PlayerId { get; set; }
    public int TypeCharacter { get; set; }
}

public class CommandUpdate
{
    public string TypeCommand { get; set; } // "TAKE_ORDER", "PICKUP_ORDER", "DELIVER_ORDER"
    public string OrderId { get; set; }
}
