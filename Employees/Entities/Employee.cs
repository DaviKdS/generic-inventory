namespace GenericInventory.Employees.Entities;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string LegacyImportId { get; set; } = string.Empty;
}
