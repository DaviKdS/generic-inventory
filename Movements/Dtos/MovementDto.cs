using GenericInventory.Movements.Entities;

namespace GenericInventory.Movements.Dtos;

public class MovementDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitValue { get; set; }
    public decimal TotalValue { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeSection { get; set; } = string.Empty;
    public string EmployeeRegistration { get; set; } = string.Empty;
    public string Catalyst { get; set; } = string.Empty;

    public static MovementDto FromEntity(Movement movement)
    {
        return new MovementDto
        {
            Id = movement.Id,
            Date = movement.Date,
            Type = movement.Type,
            ProductCode = movement.ProductCode,
            ProductDescription = movement.ProductDescription,
            Quantity = movement.Quantity,
            UnitValue = movement.UnitValue,
            TotalValue = movement.TotalValue,
            EmployeeName = movement.EmployeeName,
            EmployeeSection = movement.EmployeeSection,
            EmployeeRegistration = movement.EmployeeRegistration,
            Catalyst = movement.Catalyst
        };
    }
}
