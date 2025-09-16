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
        _ = OrderGeneratorLoop();
        while (_running)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }
    
    private async Task OrderGeneratorLoop()
    {
        while (_running)
        {
            try
            {
                await Task.Delay(10000);
                var newOrder = OrderManager.GenerateOrder();

                var orderUpdate = new OrderUpdate { Order = newOrder, Type = "NEW" };
                await BroadcastOrderUpdateToAll(orderUpdate);

                Console.WriteLine($"[UDP] Создан новый заказ: {newOrder.Description} ({newOrder.Reward}$)");
            }
            catch (OperationCanceledException)
            {
                break;
            }
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
                    TypeCharacter = new System.Random().Next(0, 15), 
                    Id = playerId,
                    Username = username,
                    TcpStream = stream,
                };

                PlayerManager.AddPlayer(player);
                
                var loginResponse = new LoginResponse
                {
                    PlayerId = player.Id,
                    TypeCharacter = player.TypeCharacter
                };
                
                string jsonResponse = JsonConvert.SerializeObject(loginResponse);
                byte[] data = Encoding.UTF8.GetBytes(jsonResponse);
                var lengthBytes = BitConverter.GetBytes(data.Length); 


                await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

                await stream.WriteAsync(data, 0, data.Length);

                Console.WriteLine($"[TCP] Игрок {username} ({playerId}) подключился");
               
                // ✅ Отправляем заказы по TCP
                await SendActiveOrdersToPlayerViaTcp(playerId, stream);

                // ✅ Рассылаем нового игрока всем остальным
                await BroadcastNewPlayerAsync(player);

                // ✅ Отправляем новому игроку всех остальных игроков
                await SendAllPlayersToNewPlayer(player);
                
                await SendCurrentAnimationStatesToPlayer(playerId, stream);

                // Держим соединение для команд
                while (client.Connected)
                {
                    var lengthBytesS = new byte[4];
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int read = await stream.ReadAsync(lengthBytesS, totalRead, 4 - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                    if (totalRead != 4) break;

                    int dataLength = BitConverter.ToInt32(lengthBytesS, 0);

                    // Защита от некорректной длины
                    if (dataLength <= 0 || dataLength > 1_000_000)
                    {
                        Console.WriteLine($"[TCP] Некорректная длина от клиента {playerId}: {dataLength}");
                        break;
                    }

                    // Читаем данные
                    var dataBuffer = new byte[dataLength];
                    totalRead = 0;
                    while (totalRead < dataLength)
                    {
                        int read = await stream.ReadAsync(dataBuffer, totalRead, dataLength - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                    if (totalRead != dataLength) break;

                    string json = Encoding.UTF8.GetString(dataBuffer, 0, totalRead);

                    Console.WriteLine("ПРИШЕЛ JSON: " + json + "от игрока" + playerId);
                    // Обрабатываем AnimationStateUpdate
                    if (json.Contains("\"AnimationState\""))
                    {
                        var updates = JsonConvert.DeserializeObject<AnimationStateUpdate[]>(json);
                        foreach (var animUpdate in updates)
                        {
                            if (animUpdate.PlayerId == playerId)
                            {
                                HandleAnimationState(playerId, animUpdate.AnimationState);
                            }
                        }
                        continue;
                    }

                    // Обрабатываем команды
                    HandleCommand(json, playerId);
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
    
    private void HandleAnimationState(string playerId, string animState)
    {
        var player = PlayerManager.GetPlayer(playerId);
        if (player != null)
        {
            player.AnimationState = animState; // сохраняем для новых игроков
        }

        var update = new AnimationStateUpdate
        {
            PlayerId = playerId,
            AnimationState = animState
        };

        _ = BroadcastAnimationState(update);
    }

    private async Task BroadcastAnimationState(AnimationStateUpdate update)
    {
        var animUpdate = new AnimationStateUpdate { PlayerId = update.PlayerId, AnimationState = update.AnimationState }; 
        var animUpdates = new[] { animUpdate };
        var json = JsonConvert.SerializeObject(animUpdates);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.TcpStream == null || !player.TcpStream.CanWrite) continue;

            try
            {
                await player.TcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await player.TcpStream.WriteAsync(data, 0, data.Length);
            }
            catch { /* ... */ }
        }
    }
    
    private async Task SendCurrentAnimationStatesToPlayer(string newPlayerId, NetworkStream stream)
    {
        var updates = new List<AnimationStateUpdate>();

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.Id == newPlayerId) continue;

            updates.Add(new AnimationStateUpdate
            {
                PlayerId = player.Id,
                AnimationState = player.AnimationState
            });
        }

        // ✅ Если нет игроков — не отправляем
        if (updates.Count == 0) return;

        // ✅ Сериализуем весь массив
        var json = JsonConvert.SerializeObject(updates);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await stream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[TCP] ✅ Отправлено {updates.Count} состояний анимаций новому игроку {newPlayerId}");
    }
    
    public async Task BroadcastOrderUpdateToAll(OrderUpdate orderUpdate)
    {
        var updates = new[] { orderUpdate }; // ← массив из одного элемента!
        var json = JsonConvert.SerializeObject(updates);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.TcpStream == null || !player.TcpStream.CanWrite) continue;

            try
            {
                await player.TcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await player.TcpStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] Ошибка отправки заказа игроку {player.Id}: {ex.Message}");
            }
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

        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await stream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[TCP] ✅ Отправлено {activeOrders.Length} заказов игроку {playerId} по TCP");
    }

    private async Task BroadcastNewPlayerAsync(Player playerNew)
    {
        var newPlayer = new NewPlayerUpdate()
        {
            MessageType = "NewPlayer",
            TypeCharacter = playerNew.TypeCharacter,
            PlayerId = playerNew.Id,
            Username = playerNew.Username,
            X = playerNew.X,
            Y = playerNew.Y,
            Z = playerNew.Z,
            Yaw = playerNew.Yaw
        };
        var updatesNewPlayer = new[] { newPlayer };
        var json = JsonConvert.SerializeObject(updatesNewPlayer);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);
        
        foreach (var player in PlayerManager.GetAllPlayers())
        {
            if (player.Id == playerNew.Id) continue;

            
            if (player.TcpStream == null || !player.TcpStream.CanWrite) continue;

            try
            {
                await player.TcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await player.TcpStream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"[TCP] ✅ Игрок {newPlayer.Username} ({playerNew.Id}) подключился — уведомляем игрока {player.Username} ({player.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] ❌ Ошибка отправки игроку {player.Id}: {ex.Message}");
            }
        }
    }
    

    private async Task SendAllPlayersToNewPlayer(Player playerNew)
    {
        var currentPlayerUpdates = new List<NewPlayerUpdate>();
        var allPlayers = PlayerManager.GetAllPlayers();

        foreach (var player in allPlayers)
        {
            if (player.Id == playerNew.Id) continue; // не отправляем самого себя

            var playerUpdate = new NewPlayerUpdate
            {
                MessageType = "NewPlayer", // ← важно для клиента!
                PlayerId = player.Id,
                Username = player.Username,
                X = player.X,
                Y = player.Y,
                Z = player.Z,
                Yaw = player.Yaw,
                TypeCharacter = player.TypeCharacter // ← отправляем один раз!
            };

            currentPlayerUpdates.Add(playerUpdate);
        }
        if (currentPlayerUpdates.Count == 0)
        {
            Console.WriteLine($"[TCP] Нет игроков для отправки новому игроку {playerNew.Id}");
            return;
        }
        // Сериализуем массив
        var json = JsonConvert.SerializeObject(currentPlayerUpdates);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        // Отправляем длину + данные
        await playerNew.TcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await playerNew.TcpStream.WriteAsync(data, 0, data.Length);

        Console.WriteLine($"[TCP] ✅ Отправлено {currentPlayerUpdates.Count} игроков новому игроку {playerNew.Id} по TCP");
    }
    
    
    private async Task HandleTakeOrder(string orderId, string playerId)
    {
        var order = OrderManager.GetOrder(orderId);
        var player = PlayerManager.GetPlayer(playerId);
        if (order != null && string.IsNullOrEmpty(order.TakenByPlayerId) && player != null)
        {
            bool changed = OrderManager.UpdateOrder(orderId, o => o.TakenByPlayerId = playerId);
            if (changed)
            {
                await BroadcastOrderUpdateToAll(new OrderUpdate { Order = order, Type = "UPDATE" });
                Console.WriteLine($"[TCP] Игрок {playerId} взял заказ {orderId}");
            }
        }
    }
    
    private async Task HandlePickupOrder(string orderId, string playerId)
    {
        var order = OrderManager.GetOrder(orderId);
        var player = PlayerManager.GetPlayer(playerId);
        if (order != null && order.TakenByPlayerId == playerId && player != null)
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
                    await BroadcastOrderUpdateToAll(new OrderUpdate { Order = order, Type = "UPDATE" });
                    Console.WriteLine($"[TCP] Игрок {playerId} забрал заказ {orderId}");
                }
            }
        }
    }
    
    private async Task HandleDeliverOrder(string orderId, string playerId)
    {
        var order = OrderManager.GetOrder(orderId);
        var player = PlayerManager.GetPlayer(playerId);
        if (order != null && order.TakenByPlayerId == playerId && player != null)
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
                    await BroadcastOrderUpdateToAll(new OrderUpdate { Order = order, Type = "UPDATE" });
                    Console.WriteLine($"[TCP] Игрок {playerId} доставил заказ {orderId}");
                }
            }
        }
    }
    

    private async void HandleCommand(string command, string playerId)
    {
        var commandUpdate = JsonConvert.DeserializeObject<CommandUpdate>(command);

        switch (commandUpdate.TypeCommand)
        {
            case "TAKE_ORDER":
                await HandleTakeOrder(commandUpdate.OrderId, playerId);
                break;
            case "PICKUP_ORDER":
                await HandlePickupOrder(commandUpdate.OrderId, playerId);
                break;
            case "DELIVER_ORDER":
                await HandleDeliverOrder(commandUpdate.OrderId, playerId);
                break;
            case "EXIT":
                HandleExitCommand(playerId);
                break;
            default:
                Console.WriteLine($"[TCP] Неизвестная команда: {commandUpdate.TypeCommand}");
                break;
            
        }
    }
    
    private void HandleExitCommand(string playerId)
    {
        PlayerManager.RemovePlayer(playerId);
    }

    public void Stop() => _listener?.Stop();
}