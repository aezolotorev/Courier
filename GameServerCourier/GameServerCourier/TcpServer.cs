using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class TcpServer
{
     private TcpListener _listener;
    private bool _running = true;
    private UdpServer _udpServer;

    public TcpServer(UdpServer udpServer)
    {
        _udpServer = udpServer;
    }

    public async Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Console.WriteLine($"[TCP] Сервер запущен на порту {port}");

        while (_running)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[1024];
        string playerId = "";
        string username = "Courier";

        try
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (message.StartsWith("LOGIN:"))
            {
                username = message["LOGIN:".Length..].Trim();
                playerId = Guid.NewGuid().ToString();

                var player = SaveManager.LoadPlayer(playerId) ?? new Player
                {
                    Id = playerId,
                    Username = username
                };

                PlayerManager.AddPlayer(player);

                string response = $"ID:{playerId}\n";
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                Console.WriteLine($"[TCP] Игрок {username} ({playerId}) подключился");
               
                // ✅ Отправляем заказы по TCP
                await SendActiveOrdersToPlayerViaTcp(playerId, stream);

                // ✅ Рассылаем нового игрока всем остальным
                BroadcastNewPlayer(player);

                // ✅ Отправляем новому игроку всех остальных игроков
                SendAllPlayersToNewPlayer(playerId);

                // Держим соединение для команд
                while (client.Connected)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    HandleCommand(message, playerId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCP] Ошибка: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                var player = PlayerManager.GetPlayer(playerId);
                if (player != null)
                {
                    SaveManager.SavePlayer(player);
                    PlayerManager.RemovePlayer(playerId);
                }
            }
            client.Close();
            Console.WriteLine($"[TCP] Клиент {playerId} отключён");
        }
    }

    private async Task SendActiveOrdersToPlayerViaTcp(string playerId, NetworkStream stream)
    {
        var activeOrders = OrderManager.GetAllActiveOrders()
            .Where(o => string.IsNullOrEmpty(o.TakenByPlayerId) || o.TakenByPlayerId == playerId)
            .Select(o => new OrderUpdate { Order = o, Type = "UPDATE" })
            .ToArray();

        var json = JsonConvert.SerializeObject(activeOrders);
        var data = Encoding.UTF8.GetBytes(json);

        var lengthBytes = BitConverter.GetBytes(data.Length);
    
        // 👇👇👇 ДОБАВЬ ЭТИ ЛОГИ
        Console.WriteLine($"[TCP] DEBUG: Serialized {activeOrders.Length} orders, JSON length: {data.Length} bytes");
        Console.WriteLine($"[TCP] DEBUG: First 4 length bytes (as int32): {BitConverter.ToInt32(lengthBytes, 0)}");
        Console.WriteLine($"[TCP] DEBUG: JSON: {json}");

        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await stream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[TCP] ✅ Отправлено {activeOrders.Length} заказов игроку {playerId} по TCP");
    }

    private void BroadcastNewPlayer(Player newPlayer)
    {
        var playerUpdate = new PlayerUpdate
        {
            PlayerId = newPlayer.Id,
            Username = newPlayer.Username,
            X = newPlayer.X,
            Y = newPlayer.Y,
            Z = newPlayer.Z,
            Yaw = newPlayer.Yaw
        };

        var json = JsonConvert.SerializeObject(playerUpdate);
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.Id == newPlayer.Id || player.UdpEndpoint == null) continue;

            _ = _udpServer.SendToEndpoint(data, player.UdpEndpoint);
        }
    }

    private void SendAllPlayersToNewPlayer(string newPlayerId)
    {
        var allPlayers = PlayerManager.GetAllPlayers();
        foreach (var player in allPlayers)
        {
            if (player.Id == newPlayerId) continue;

            var playerUpdate = new PlayerUpdate
            {
                PlayerId = player.Id,
                Username = player.Username,
                X = player.X,
                Y = player.Y,
                Z = player.Z,
                Yaw = player.Yaw
            };

            var json = JsonConvert.SerializeObject(playerUpdate);
            var data = Encoding.UTF8.GetBytes(json);

            var targetPlayer = PlayerManager.GetPlayer(newPlayerId);
            if (targetPlayer?.UdpEndpoint != null)
            {
                _ = _udpServer.SendToEndpoint(data, targetPlayer.UdpEndpoint);
            }
        }
    }

    private void HandleCommand(string command, string playerId)
    {
        if (command.StartsWith("TAKE_ORDER:"))
        {
            var orderId = command["TAKE_ORDER:".Length..];
            var order = OrderManager.GetOrder(orderId);
            var player = PlayerManager.GetPlayer(playerId);
            if (order != null && string.IsNullOrEmpty(order.TakenByPlayerId) && player != null)
            {
                bool changed = OrderManager.UpdateOrder(orderId, o => o.TakenByPlayerId = playerId);
                if (changed)
                {
                    Console.WriteLine($"[TCP] Игрок {playerId} взял заказ {orderId}");
                    _udpServer.BroadcastOrderUpdate(new OrderUpdate { Order = order, Type = "UPDATE" });
                }
            }
        }
        else if (command.StartsWith("PICKUP_ORDER:"))
        {
            var orderId = command["PICKUP_ORDER:".Length..];
            var order = OrderManager.GetOrder(orderId);
            var player = PlayerManager.GetPlayer(playerId);
            if (order != null && order.TakenByPlayerId == playerId && player != null && !order.IsPickedUp)
            {
                float distance = (float)Math.Sqrt(
                    Math.Pow(player.X - order.PickupX, 2) +
                    Math.Pow(player.Y - order.PickupY, 2) +
                    Math.Pow(player.Z - order.PickupZ, 2)
                );

                if (distance <= 3.0f)
                {
                    bool changed = OrderManager.UpdateOrder(orderId, o => o.IsPickedUp = true);
                    if (changed)
                    {
                        Console.WriteLine($"[TCP] Игрок {playerId} подобрал заказ {orderId}");
                        _udpServer.BroadcastOrderUpdate(new OrderUpdate { Order = order, Type = "UPDATE" });
                    }
                }
            }
        }
        else if (command.StartsWith("DELIVER_ORDER:"))
        {
            var orderId = command["DELIVER_ORDER:".Length..];
            var order = OrderManager.GetOrder(orderId);
            var player = PlayerManager.GetPlayer(playerId);
            if (order != null && order.TakenByPlayerId == playerId && player != null && order.IsPickedUp && !order.IsCompleted)
            {
                float distance = (float)Math.Sqrt(
                    Math.Pow(player.X - order.DropoffX, 2) +
                    Math.Pow(player.Y - order.DropoffY, 2) +
                    Math.Pow(player.Z - order.DropoffZ, 2)
                );

                if (distance <= 3.0f)
                {
                    bool changed = OrderManager.UpdateOrder(orderId, o => o.IsCompleted = true);
                    if (changed)
                    {
                        player.Money += order.Reward;
                        player.DeliveriesCompleted++;
                        OrderManager.CompleteOrder(orderId);
                        Console.WriteLine($"[TCP] Игрок {playerId} доставил заказ {orderId}");
                        _udpServer.BroadcastOrderUpdate(new OrderUpdate { Order = order, Type = "UPDATE" });
                    }
                }
            }
        }
        else if (command == "EXIT")
        {
            Console.WriteLine($"[TCP] Игрок {playerId} запросил выход");
            PlayerManager.RemovePlayer(playerId);
        }
    }

    public void Stop() => _listener?.Stop();
}