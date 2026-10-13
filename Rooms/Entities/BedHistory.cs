namespace GenericInventory.Rooms.Entities;

public class BedHistory
{
    public int Id { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
    public string BedId { get; set; } = string.Empty;
    public string AreaName { get; set; } = "Camas";
    public string RoomLabel { get; set; } = string.Empty;
    public string Status { get; set; } = "available";
    public string GuestCode { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
}
