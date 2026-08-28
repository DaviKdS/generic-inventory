namespace GenericInventory.Movements.Dtos;

public class StockMovementRequestDto
{
    public string ProductCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int? EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeSection { get; set; } = string.Empty;
    public string EmployeeRegistration { get; set; } = string.Empty;
    public string Catalyst { get; set; } = string.Empty;
}
