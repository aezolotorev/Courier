using System;

[Serializable]
public class Order
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "Посылка";
    public float PickupX { get; set; }
    public float PickupY { get; set; }
    public float PickupZ { get; set; }
    public float DropoffX { get; set; }
    public float DropoffY { get; set; }
    public float DropoffZ { get; set; }
    public int Reward { get; set; } = 50;
    public string TakenByPlayerId { get; set; } // убери ? если в юнити не поддерживается
    public bool IsPickedUp { get; set; }
    public bool IsCompleted { get; set; }
}