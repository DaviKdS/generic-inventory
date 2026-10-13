namespace GenericInventory.Rooms.Entities;

public class Bed
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1;
    public string Status { get; set; } = "available";
    public string GuestName { get; set; } = string.Empty;
    public string GuestCode { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string EmergencyContact { get; set; } = string.Empty;
    public string EmergencyPhone { get; set; } = string.Empty;
    public string BloodType { get; set; } = string.Empty;
    public string CheckIn { get; set; } = string.Empty;
    public string CheckOut { get; set; } = string.Empty;
    public string ActualCheckOut { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public bool Fixed { get; set; }
    public DateTimeOffset? OccupancyStartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
