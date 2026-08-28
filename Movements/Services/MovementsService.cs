using Microsoft.EntityFrameworkCore;
using GenericInventory.Data;
using GenericInventory.Employees.Entities;
using GenericInventory.Movements.Dtos;
using GenericInventory.Movements.Entities;
using GenericInventory.Products.Entities;
using GenericInventory.Reminders.Services;

namespace GenericInventory.Movements.Services;

public class MovementsService
{
    public const string StockIn = "Entrada";
    public const string StockOut = "Saida";

    private readonly AppDbContext _db;
    private readonly StockReminderService _reminderService;

    public MovementsService(AppDbContext db, StockReminderService reminderService)
    {
        _db = db;
        _reminderService = reminderService;
    }

    public async Task<IReadOnlyList<MovementDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Movements
            .AsNoTracking()
            .OrderByDescending(movement => movement.Date)
            .ThenByDescending(movement => movement.Id)
            .Select(movement => MovementDto.FromEntity(movement))
            .ToListAsync(cancellationToken);
    }

    public Task<MovementDto> CreateStockInAsync(StockMovementRequestDto request, CancellationToken cancellationToken = default)
    {
        return CreateAsync(StockIn, request, cancellationToken);
    }

    public Task<MovementDto> CreateStockOutAsync(StockMovementRequestDto request, CancellationToken cancellationToken = default)
    {
        return CreateAsync(StockOut, request, cancellationToken);
    }

    private async Task<MovementDto> CreateAsync(string type, StockMovementRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Quantity < 1 || request.Quantity > 1000)
        {
            throw new InvalidOperationException("A quantidade deve ser um numero entre 1 e 1000.");
        }

        var product = await _db.Products.FirstOrDefaultAsync(item => item.Code == request.ProductCode.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("Produto nao encontrado.");

        if (type == StockOut && request.Quantity > product.CurrentStock)
        {
            throw new InvalidOperationException("Estoque insuficiente para esta saida.");
        }

        var employee = request.EmployeeId is > 0
            ? await _db.Employees.FirstOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            : null;

        var movement = BuildMovement(type, request, product, employee);
        product.CurrentStock = type == StockIn
            ? product.CurrentStock + request.Quantity
            : product.CurrentStock - request.Quantity;

        _db.Movements.Add(movement);
        await _db.SaveChangesAsync(cancellationToken);
        await _reminderService.SendMovementAlertsAsync(product, cancellationToken);
        return MovementDto.FromEntity(movement);
    }

    private static Movement BuildMovement(string type, StockMovementRequestDto request, Product product, Employee? employee)
    {
        var employeeName = employee?.Name ?? request.EmployeeName.Trim();
        var employeeSection = employee?.Section ?? request.EmployeeSection.Trim();
        var employeeRegistration = employee?.Registration ?? request.EmployeeRegistration.Trim();
        var unitValue = product.SaleValue;

        return new Movement
        {
            Date = DateTime.Today,
            Type = type,
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductDescription = product.Description,
            Quantity = request.Quantity,
            UnitValue = unitValue,
            TotalValue = unitValue * request.Quantity,
            EmployeeId = employee?.Id,
            EmployeeName = employeeName,
            EmployeeSection = employeeSection,
            EmployeeRegistration = employeeRegistration,
            Catalyst = string.IsNullOrWhiteSpace(request.Catalyst) ? product.Catalyst : request.Catalyst.Trim()
        };
    }
}
