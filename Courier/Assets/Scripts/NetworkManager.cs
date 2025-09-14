using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks; // ← добавь
using UnityEngine;

public class NetworkManager : MonoBehaviour
{

    public static NetworkManager Instance;

    public string ServerIP = "127.0.0.1";
    public int TcpPort = 7777;
    public int UdpPort = 7778;


    private TcpClient _tcpClient;
    private NetworkStream _tcpStream;
    private UdpClient _udpClient;
    private IPEndPoint _udpServerEP;
    private CancellationTokenSource _cancellationTokenSource;

    public string PlayerId { get; private set; } = "";
    public string Username = "Courier";

    private readonly Dictionary<string, GameObject> _remotePlayers = new();
    private bool _isInitialized = false;
     private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

     private void Start()
    {
        ConnectToServer();
    }

    // ======================
    // Подключение
    // ======================

    public async void ConnectToServer()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(ServerIP, TcpPort);
            _tcpStream = _tcpClient.GetStream();

            // Отправляем LOGIN
            string loginMessage = $"LOGIN:{Username}\n";
            byte[] loginBytes = Encoding.UTF8.GetBytes(loginMessage);
            await _tcpStream.WriteAsync(loginBytes, 0, loginBytes.Length);

            // Читаем ID
            string response = await ReadLineAsync(_tcpStream, _cancellationTokenSource.Token);

            if (response.StartsWith("ID:"))
            {
                PlayerId = response["ID:".Length..].Trim();
                Debug.Log($"[TCP] Получен ID: {PlayerId}");

                // ✅ ДАЁМ СЕРВЕРУ ВРЕМЯ ОТПРАВИТЬ ЗАКАЗЫ
                await UniTask.Delay(100); // Пауза 100 мс

                // ✅ Читаем заказы
                await ReadInitialOrders();

                // ✅ Запускаем фоновое прослушивание TCP
                _ = ListenTcpAsync(_cancellationTokenSource.Token);

                // ✅ Запускаем UDP
                _udpClient = new UdpClient(0);
                _udpServerEP = new IPEndPoint(IPAddress.Parse(ServerIP), UdpPort);
                _ = ListenUdpAsync(_cancellationTokenSource.Token);

                _isInitialized = true;
                SpawnPlayer();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Ошибка подключения: " + ex.Message);
        }
    }
    
    private async UniTask<string> ReadLineAsync(NetworkStream stream, CancellationToken token = default)
    {
        using (var ms = new MemoryStream())
        {
            byte[] buffer = new byte[1];
            while (await stream.ReadAsync(buffer, 0, 1, token) > 0)
            {
                if (buffer[0] == '\n')
                    break;
                if (buffer[0] != '\r') // игнорируем \r
                    ms.Write(buffer, 0, 1);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    // ======================
    // Чтение начальных заказов (с защитой и логами)
    // ======================

    private async UniTask ReadInitialOrders()
    {
        try
        {
            // ✅ Ждём, пока данные не появятся (максимум 1 сек)
            int waitTimeMs = 0;
            while (_tcpClient.Available == 0 && waitTimeMs < 1000)
            {
                await UniTask.Delay(10);
                waitTimeMs += 10;
            }

            if (_tcpClient.Available == 0)
            {
                Debug.LogWarning("[TCP] Сервер не отправил данные заказов в течение 1 секунды. Пропускаем.");
                return;
            }

            // ✅ Читаем 4 байта длины
            var lengthBytes = new byte[4];
            int totalRead = 0;
            while (totalRead < 4 && _tcpClient?.Connected == true)
            {
                int read = await _tcpStream.ReadAsync(lengthBytes, totalRead, 4 - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead != 4)
            {
                Debug.LogError("[TCP] Не удалось прочитать 4 байта длины заказов");
                return;
            }

            int dataLength = BitConverter.ToInt32(lengthBytes, 0);

            // 🛡️ Защита от некорректной длины
            if (dataLength <= 0 || dataLength > 1_000_000) // 1 МБ максимум
            {
                string hex = BitConverter.ToString(lengthBytes).Replace("-", " ");
                Debug.LogError($"[TCP] ОШИБКА: Получена некорректная длина данных ({dataLength} байт). Hex: {hex}");
                return;
            }

            Debug.Log($"[TCP] Ожидаем {dataLength} байт данных заказов");

            // ✅ Читаем данные
            var dataBuffer = new byte[dataLength];
            totalRead = 0;
            while (totalRead < dataLength && _tcpClient?.Connected == true)
            {
                int read = await _tcpStream.ReadAsync(dataBuffer, totalRead, dataLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead != dataLength)
            {
                Debug.LogError($"[TCP] Прочитано только {totalRead} из {dataLength} байт");
                return;
            }

            string json = Encoding.UTF8.GetString(dataBuffer);
            Debug.Log($"[TCP] Получен JSON заказов (длина: {json.Length}): {json}");

            var orderUpdates = JsonConvert.DeserializeObject<OrderUpdate[]>(json);
            if (orderUpdates == null)
            {
                Debug.LogError("[TCP] Десериализация заказов вернула NULL");
                return;
            }

            Debug.Log($"[TCP] ✅ Успешно десериализовано {orderUpdates.Length} заказов");

            foreach (var update in orderUpdates)
            {
                if (update?.Order != null)
                {
                    Debug.Log($"[TCP] Заказ: {update.Order.Description} (ID: {update.Order.Id}, Награда: {update.Order.Reward})");
                    UIManager.Instance?.HandleOrderUpdate(update);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCP] Ошибка при чтении начальных заказов: {ex.Message}");
        }
    }

    // ======================
    // TCP Listener (фоновый)
    // ======================

    private async UniTask ListenTcpAsync(CancellationToken token)
    {
        while (_tcpClient?.Connected == true && !token.IsCancellationRequested)
        {
            try
            {
                var lengthBytes = new byte[4];
                int totalRead = 0;
                while (totalRead < 4 && !token.IsCancellationRequested)
                {
                    int read = await _tcpStream.ReadAsync(lengthBytes, totalRead, 4 - totalRead, token);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead != 4) break;

                int length = BitConverter.ToInt32(lengthBytes, 0);

                if (length <= 0 || length > 1_000_000)
                {
                    Debug.LogError($"[TCP] Некорректная длина в фоновом чтении: {length}");
                    break;
                }

                var buffer = new byte[length];
                totalRead = 0;
                while (totalRead < length && !token.IsCancellationRequested)
                {
                    int read = await _tcpStream.ReadAsync(buffer, totalRead, length - totalRead, token);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead != length) break;

                string json = Encoding.UTF8.GetString(buffer);

                if (!_isInitialized)
                {
                    Debug.LogWarning("[TCP] Получены данные до инициализации, игнорируем: " + json);
                    continue;
                }

                var orderUpdates = JsonConvert.DeserializeObject<OrderUpdate[]>(json);
                if (orderUpdates == null) continue;

                foreach (var update in orderUpdates)
                {
                    UIManager.Instance?.HandleOrderUpdate(update);
                }

                Debug.Log($"[TCP] Получено {orderUpdates.Length} обновлений заказов");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[TCP] Прием данных отменен.");
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError("TCP ошибка: " + ex.Message);
                break;
            }
        }
    }

    // ======================
    // UDP Listener
    // ======================

    private async UniTask ListenUdpAsync(CancellationToken token)
    {
        while (_udpClient != null && !token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string json = Encoding.UTF8.GetString(result.Buffer);

                if (json.Contains("\"PlayerId\"") && json.Contains("\"Yaw\""))
                {
                    var playerUpdate = JsonConvert.DeserializeObject<PlayerUpdate>(json);
                    if (playerUpdate != null && playerUpdate.PlayerId != PlayerId)
                    {
                        HandleRemotePlayerUpdate(playerUpdate);
                    }
                }

                if (json.Contains("\"Order\""))
                {
                    Debug.Log($"[UDP] Получен JSON заказа: {json} обновление по udp");
                    var orderUpdate = JsonConvert.DeserializeObject<OrderUpdate>(json);
                    if (orderUpdate != null)
                    {
                        UIManager.Instance?.HandleOrderUpdate(orderUpdate);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[UDP] Прием данных отменен.");
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError("UDP ошибка: " + ex.Message);
            }
        }
    }

    // ======================
    // Отправка данных
    // ======================

    public async void SendPositionAndRotation(Vector3 position, float yaw)
    {
        if (string.IsNullOrEmpty(PlayerId) || _udpClient == null || _udpServerEP == null) return;

        var update = new PlayerUpdate
        {
            PlayerId = PlayerId,
            X = position.x,
            Y = position.y,
            Z = position.z,
            Yaw = yaw
        };

        string json = JsonConvert.SerializeObject(update);
        byte[] data = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(data, data.Length, _udpServerEP);
    }

    private void HandleRemotePlayerUpdate(PlayerUpdate update)
    {
        if (!_remotePlayers.TryGetValue(update.PlayerId, out var playerObj))
        {
            var playerPrefab = Resources.Load<GameObject>("RemotePlayer");
            if (playerPrefab != null)
            {
                playerObj = Instantiate(playerPrefab, new Vector3(update.X, update.Y, update.Z), Quaternion.identity);
                playerObj.name = "Player_" + update.Username;
                var remoteComp = playerObj.GetComponent<RemotePlayer>();
                if (remoteComp != null)
                {
                    remoteComp.PlayerId = update.PlayerId;
                    remoteComp.Username = update.Username;
                }
                _remotePlayers[update.PlayerId] = playerObj;
            }
        }

        playerObj?.GetComponent<RemotePlayer>()?.UpdateState(update.X, update.Y, update.Z, update.Yaw);
    }

    private void SpawnPlayer()
    {
        var playerPrefab = Resources.Load<GameObject>("Player");
        if (playerPrefab != null)
        {
            Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("Игрок создан!");
        }
        else
        {
            Debug.LogError("Не найден префаб Player в Resources!");
        }
    }

    // ======================
    // Команды игроков
    // ======================

    public async void TakeOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;

        string command = $"TAKE_ORDER:{orderId}";
        byte[] data = Encoding.UTF8.GetBytes(command);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);
        Debug.Log($"[TCP] Отправлена команда TAKE_ORDER для заказа {orderId}");
    }

    public async void PickupOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;

        string command = $"PICKUP_ORDER:{orderId}";
        byte[] data = Encoding.UTF8.GetBytes(command);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);
        Debug.Log($"[TCP] Отправлена команда PICKUP_ORDER для заказа {orderId}");
    }

    public async void DeliverOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;

        string command = $"DELIVER_ORDER:{orderId}";
        byte[] data = Encoding.UTF8.GetBytes(command);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);
        Debug.Log($"[TCP] Отправлена команда DELIVER_ORDER для заказа {orderId}");
    }

    // ======================
    // Отключение
    // ======================

    public void DisconnectFromServer()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            Debug.Log("[Network] Отключение от сервера...");
            _cancellationTokenSource.Cancel();
        }

        _tcpClient?.Close();
        _udpClient?.Close();

        PlayerId = "";
        Debug.Log("[Network] Отключено.");
    }

    private void OnApplicationQuit() => DisconnectFromServer();
    private void OnDestroy() => DisconnectFromServer();
}