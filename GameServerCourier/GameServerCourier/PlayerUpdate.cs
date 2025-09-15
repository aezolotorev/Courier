public class PlayerUpdate
{
    public string MessageType { get; set; } = "PositionUpdate"; // или "NewPlayer"
    public string PlayerId { get; set; } = "";
    public string Username { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; } // поворот
}