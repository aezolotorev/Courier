public interface ITcpMessageHandler
{
    bool CanHandle(string json);
    void Handle(string json);
}