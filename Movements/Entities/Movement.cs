namespace GenericInventory.Movements.Entities;

public class Movement
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Type { get; set; } = string.Empty;
    public int? ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitValue { get; set; }
    public decimal TotalValue { get; set; }
    public int? EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeSection { get; set; } = string.Empty;
    public string EmployeeRegistration { get; set; } = string.Empty;
    public string Catalyst { get; set; } = string.Empty;
    public string LegacyImportId { get; set; } = string.Empty;
}
