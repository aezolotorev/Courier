using System;

[Serializable]
public class OrderUpdate
{
    public Order Order { get; set; }
    public string Type { get; set; }
}