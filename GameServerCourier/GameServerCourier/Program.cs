using System.Text;


class Program
{
    static async Task Main(string[] args)
    {
       
        var udpServer = new UdpServer();
        var tcpServer = new TcpServer(udpServer);
       
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🚚 Запуск сервера игры \"Курьер\"...");
        Console.WriteLine("Нажмите Ctrl+C для остановки");

        var tcpTask = tcpServer.StartAsync(7777);
        var udpTask = udpServer.StartAsync(7778);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            tcpServer.Stop();
            udpServer.Stop();
            Console.WriteLine("\nСервер остановлен.");
        };

        await Task.WhenAll(tcpTask, udpTask);
    }
}
