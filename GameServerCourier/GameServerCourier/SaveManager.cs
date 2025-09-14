using System.Text.Json;

public static class SaveManager
{
    private static readonly string SavePath = "saves/";

    static SaveManager()
    {
        Directory.CreateDirectory(SavePath);
    }

    public static void SavePlayer(Player player)
    {
        var json = JsonSerializer.Serialize(player, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText($"{SavePath}{player.Id}.json", json);
    }

    public static Player? LoadPlayer(string playerId)
    {
        var path = $"{SavePath}{playerId}.json";
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Player>(json);
    }
}

