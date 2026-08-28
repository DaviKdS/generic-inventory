using Microsoft.EntityFrameworkCore;
using GenericInventory.Data;
using GenericInventory.Employees.Dtos;
using GenericInventory.Employees.Entities;

namespace GenericInventory.Employees.Services;

public class EmployeesService
{
    private readonly AppDbContext _db;

    public EmployeesService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Employees.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(employee =>
                employee.Name.Contains(term) ||
                employee.Registration.Contains(term) ||
                employee.Section.Contains(term));
        }

        return await query
            .OrderBy(employee => employee.Name)
            .Select(employee => EmployeeDto.FromEntity(employee))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeDto> CreateAsync(EmployeeFormDto form, CancellationToken cancellationToken = default)
    {
        Validate(form);
        var registration = form.Registration.Trim();
        if (await _db.Employees.AnyAsync(employee => employee.Registration == registration, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe funcionario com esta matricula.");
        }

        var employee = new Employee();
        Apply(employee, form);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);
        return EmployeeDto.FromEntity(employee);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, EmployeeFormDto form, CancellationToken cancellationToken = default)
    {
        Validate(form);
        var employee = await FindAsync(id, cancellationToken);
        var registration = form.Registration.Trim();
        if (!string.Equals(employee.Registration, registration, StringComparison.OrdinalIgnoreCase) &&
            await _db.Employees.AnyAsync(item => item.Registration == registration, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe funcionario com esta matricula.");
        }

        Apply(employee, form);
        await _db.SaveChangesAsync(cancellationToken);
        return EmployeeDto.FromEntity(employee);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await FindAsync(id, cancellationToken);
        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Employee> FindAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Funcionario nao encontrado.");
    }

    private static void Validate(EmployeeFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            throw new InvalidOperationException("Informe o nome do funcionario.");
        }

        if (string.IsNullOrWhiteSpace(form.Registration))
        {
            throw new InvalidOperationException("Informe a matricula do funcionario.");
        }
    }

    private static void Apply(Employee employee, EmployeeFormDto form)
    {
        employee.Name = form.Name.Trim();
        employee.Registration = form.Registration.Trim();
        employee.Section = form.Section.Trim();
    }
}
