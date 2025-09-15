using System.Net;
using System.Net.Sockets;

public class Player
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "Courier";
    public IPEndPoint? UdpEndpoint { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; } 
    public int Money { get; set; } = 100; // стартовые деньги
    public string? CurrentOrderId { get; set; } // ID текущего заказа
    public int DeliveriesCompleted { get; set; } = 0;
    public NetworkStream TcpStream { get; set; }
}