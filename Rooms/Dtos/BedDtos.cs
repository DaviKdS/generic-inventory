using GenericInventory.Rooms.Entities;

namespace GenericInventory.Rooms.Dtos;

public class BedReservationsDto
{
    public int ModelVersion { get; set; } = 4;
    public List<BedAreaDto> Areas { get; set; } = new();
    public List<BedHistoryDto> History { get; set; } = new();
}

public class BedAreaDto
{
    public string Id { get; set; } = "camas";
    public string Name { get; set; } = "Camas";
    public List<BedDto> Rooms { get; set; } = new();
}

public class BedDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string ReservationCode { get; set; } = string.Empty;
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
    public DateTimeOffset UpdatedAt { get; set; }

    public static BedDto FromEntity(Bed bed)
    {
        return new BedDto
        {
            Id = bed.Id,
            Label = bed.Label,
            Category = bed.Category,
            Capacity = bed.Capacity,
            Status = bed.Status,
            GuestName = bed.GuestName,
            ReservationCode = bed.GuestCode,
            Cpf = bed.Cpf,
            Phone = bed.Phone,
            EmergencyContact = bed.EmergencyContact,
            EmergencyPhone = bed.EmergencyPhone,
            BloodType = bed.BloodType,
            CheckIn = bed.CheckIn,
            CheckOut = bed.CheckOut,
            ActualCheckOut = bed.ActualCheckOut,
            Note = bed.Note,
            Operator = bed.Operator,
            Fixed = bed.Fixed,
            OccupancyStartedAt = bed.OccupancyStartedAt,
            UpdatedAt = bed.UpdatedAt
        };
    }
}

public class BedHistoryDto
{
    public DateTimeOffset Date { get; set; }
    public string AreaName { get; set; } = "Camas";
    public string RoomLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReservationCode { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;

    public static BedHistoryDto FromEntity(BedHistory history)
    {
        return new BedHistoryDto
        {
            Date = history.Date,
            AreaName = history.AreaName,
            RoomLabel = history.RoomLabel,
            Status = history.Status,
            ReservationCode = history.GuestCode,
            Note = history.Note,
            Operator = history.Operator
        };
    }
}

public class BedCreateDto
{
    public string RoomLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1;
}

public class BedUpdateDto
{
    public string RoomLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1;
    public string Status { get; set; } = "available";
    public string GuestName { get; set; } = string.Empty;
    public string ReservationCode { get; set; } = string.Empty;
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
}
