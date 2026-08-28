using GenericInventory.Employees.Entities;

namespace GenericInventory.Employees.Dtos;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;

    public static EmployeeDto FromEntity(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Registration = employee.Registration,
            Section = employee.Section
        };
    }
}
