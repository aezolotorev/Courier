using System.Collections.Concurrent;
public static class PlayerManager
{
    private static readonly ConcurrentDictionary<string, Player> Players = new();

    public static void AddPlayer(Player player)
    {
        Console.WriteLine("\n добавлен игрок в список."+player.Id);
        Players.TryAdd(player.Id, player);
        Console.WriteLine("\n players count: " + Players.Count);
    }

    public static Player? GetPlayer(string id) => Players.GetValueOrDefault(id);
    public static Player[] GetAllPlayers() => Players.Values.ToArray();
    public static void RemovePlayer(string id) => Players.TryRemove(id, out _);
    public static void UpdatePlayerPosition(string id, float x, float y, float z)
    {
        if (Players.TryGetValue(id, out var player))
        {
            player.X = x;
            player.Y = y;
            player.Z = z;
        }
    }
}