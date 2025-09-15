using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

public class UdpServer
{
     private UdpClient _udp;
    private bool _running = true;
    private CancellationTokenSource _cancellationTokenSource;

    public UdpServer()
    {
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(int port)
    {
        _udp = new UdpClient(port);
        Console.WriteLine($"[UDP] Сервер запущен на порту {port}");

        

        while (_running)
        {
            try
            {
                UdpReceiveResult result = await _udp.ReceiveAsync(_cancellationTokenSource.Token);
                string json = Encoding.UTF8.GetString(result.Buffer);

                if (json.Contains("\"PlayerId\"") && json.Contains("\"Yaw\""))
                {
                    var playerUpdate = JsonConvert.DeserializeObject<PlayerUpdate>(json);
                    if (playerUpdate != null)
                    {
                        HandlePlayerUpdate(playerUpdate, result.RemoteEndPoint);
                        continue;
                    }
                }

                if (json.Contains("\"Order\""))
                {
                    var orderUpdate = JsonConvert.DeserializeObject<OrderUpdate>(json);
                    if (orderUpdate != null)
                    {
                        HandleOrderUpdate(orderUpdate);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[UDP] Прием данных отменен.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] Ошибка: {ex.Message}");
            }
        }
    }

    private void HandlePlayerUpdate(PlayerUpdate update, IPEndPoint remoteEP)
    {
        var player = PlayerManager.GetPlayer(update.PlayerId);
        if (player == null) return;

        player.UdpEndpoint = remoteEP;
        player.Username = update.Username;
        player.X = update.X;
        player.Y = update.Y;
        player.Z = update.Z;
        player.Yaw = update.Yaw;

        Console.WriteLine($"[UDP] Обновлена позиция игрока {update.PlayerId}");

        BroadcastPlayerUpdate(update, update.PlayerId);
    }

    private async Task BroadcastPlayerUpdate(PlayerUpdate update, string senderId)
    {
        var json = JsonConvert.SerializeObject(update);
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.Id == senderId || player.UdpEndpoint == null) continue;

            await SendToEndpoint(data, player.UdpEndpoint);
        }
    }

    private void HandleOrderUpdate(OrderUpdate update)
    {
        // Обработка заказов — если нужно
    }

    public async Task SendToEndpoint(byte[] data, IPEndPoint endpoint)
    {
        try
        {
            await _udp.SendAsync(data, data.Length, endpoint.Address.ToString(), endpoint.Port);
        }
        catch { /* Игнорируем ошибки */ }
    }

    /*public async Task BroadcastOrderUpdate(OrderUpdate orderUpdate)
    {
        var json = JsonConvert.SerializeObject(orderUpdate);
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.UdpEndpoint != null)
            {
                await SendToEndpoint(data, player.UdpEndpoint);
            }
        }
    }*/

    

    public void Stop()
    {
        _running = false;
        _cancellationTokenSource.Cancel();
        _udp?.Close();
    }
}