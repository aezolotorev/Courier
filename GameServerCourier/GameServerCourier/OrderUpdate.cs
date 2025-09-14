public class OrderUpdate
{
    public Order Order { get; set; } = new();
    public string Type { get; set; } = "NEW"; // NEW, TAKEN, COMPLETED
}