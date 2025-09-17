using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
// ← добавь
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public Action<string> newPlayerConnected;
    public Action<string> playerDisconnected;
    

    public static NetworkManager Instance;

    public string ServerIP = "127.0.0.1";
    public int TcpPort = 7777;
    public int UdpPort = 7778;

    private TcpMessageHandler _tcpMessageHandler;
    private UdpMessageHandler _udpMessageHandler;
    private TcpClient _tcpClient;
    private NetworkStream _tcpStream;
    private UdpClient _udpClient;
    private IPEndPoint _udpServerEP;
    private CancellationTokenSource _cancellationTokenSource;

    public string PlayerId { get; private set; } = "";
    public string Username = "Courier";

    private readonly Dictionary<string, RemotePlayer> _remotePlayers = new();
    public Dictionary<string, RemotePlayer> RemotePlayers => _remotePlayers;
    private bool _isInitialized = false;
    [SerializeField] private CameraManager _cameraManager;
    
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
        _tcpMessageHandler = new TcpMessageHandler(this);
        _udpMessageHandler = new UdpMessageHandler(this);
        
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
            var lengthBytes = new byte[4];
            await _tcpStream.ReadAsync(lengthBytes, 0, 4);
            int length = BitConverter.ToInt32(lengthBytes, 0);

            var buffer = new byte[length];
            await _tcpStream.ReadAsync(buffer, 0, length);
            string json = Encoding.UTF8.GetString(buffer);
            Debug.Log($"[TCP] Получен JSON: '{json}'");
            var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(json);
            
            PlayerId = loginResponse.PlayerId;
            int typeCharacter = loginResponse.TypeCharacter;

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
                var player = SpawnPlayer(typeCharacter);
                _cameraManager.SetTrackingTarget(player.CameraLook);
                player.SetTrasformFormCameraController(_cameraManager.StickToObject.gameObject);
            
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
                Debug.Log($"[TCP] 🔍 ПОЛНЫЙ СЫРОЙ JSON: '{json}'");
                Debug.Log($"[TCP] 🔢 Длина: {json.Length}, Первый символ: ' {json[0]}'");
                if (!_isInitialized)
                {
                    Debug.LogWarning("[TCP] Получены данные до инициализации, игнорируем: " + json);
                    continue;
                }

                _tcpMessageHandler.HandleMessage(json);
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

                _udpMessageHandler.HandleMessage(json);
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

    public async void SendMovementUpdate(PlayerPositionUpdate positionUpdate)
    {
        if (string.IsNullOrEmpty(PlayerId) || _udpClient == null || _udpServerEP == null) return;
       
        string json = JsonConvert.SerializeObject(positionUpdate);
        byte[] data = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(data, data.Length, _udpServerEP);
    }


    private PlayerController SpawnPlayer(int typeCharacter)
    {
        var playerPrefab = Resources.Load<PlayerController>("Player");

        var player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        player.SetCharacterType(typeCharacter);

        return player;
    }

    // ======================
    // Команды игроков
    // ======================

    public async void TakeOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;
        
        var command = new CommandUpdate
        {
            TypeCommand = "TAKE_ORDER",
            OrderId = orderId
        };

        string json = JsonConvert.SerializeObject(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await _tcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);

        Debug.Log($"[TCP] Отправлена команда TAKE_ORDER для заказа {orderId}");
    }

    public async void PickupOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;

        var command = new CommandUpdate
        {
            TypeCommand = "PICKUP_ORDER",
            OrderId = orderId
        };

        string json = JsonConvert.SerializeObject(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await _tcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);

        Debug.Log($"[TCP] Отправлена команда PICKUP_ORDER для заказа {orderId}");
    }

    public async void DeliverOrder(string orderId)
    {
        if (_tcpStream == null || !_tcpClient.Connected) return;

        var command = new CommandUpdate
        {
            TypeCommand = "DELIVER_ORDER",
            OrderId = orderId
        };

        string json = JsonConvert.SerializeObject(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await _tcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);

        Debug.Log($"[TCP] Отправлена команда DELIVER_ORDER для заказа {orderId}");
    }
    
    public async void SendAnimationState(string animState)
    {
        if (string.IsNullOrEmpty(PlayerId) || _tcpStream == null) return;

        var update = new AnimationStateUpdate
        {
            PlayerId = PlayerId,
            AnimationState = animState
        };

        var updates = new[] { update };
        string json = JsonConvert.SerializeObject(updates);
        byte[] data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await _tcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _tcpStream.WriteAsync(data, 0, data.Length);
    }

    // ======================
    // Отключение
    // ======================

    public async void DisconnectFromServer()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        _tcpClient?.Close();
        _udpClient?.Close();

        
        if (_tcpStream == null || !_tcpClient.Connected) return;

        var command = new CommandUpdate
        {
            TypeCommand = "EXIT"
        };

        string json = JsonConvert.SerializeObject(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);

        await _tcpStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _tcpStream.WriteAsync(data, 0, data.Length, _cancellationTokenSource.Token);

        Debug.Log("[TCP] Отправлена команда EXIT");
        PlayerId = "";
    }

    private void OnApplicationQuit() => DisconnectFromServer();
    private void OnDestroy() => DisconnectFromServer();
}