using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Movements.Dtos;
using GenericInventory.Movements.Services;

namespace GenericInventory.Movements.Controllers;

[ApiController]
[Route("api/movements")]
public class MovementsController : ControllerBase
{
    private readonly MovementsService _service;

    public MovementsController(MovementsService service)
    {
        _service = service;
    }

    [Authorize(Policy = AccessPermissions.StockRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MovementDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.StockMove)]
    [HttpPost("stock-in")]
    public async Task<ActionResult<MovementDto>> StockIn([FromBody] StockMovementRequestDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.CreateStockInAsync(request, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.StockMove)]
    [HttpPost("stock-out")]
    public async Task<ActionResult<MovementDto>> StockOut([FromBody] StockMovementRequestDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.CreateStockOutAsync(request, cancellationToken));
    }

    private async Task<ActionResult<MovementDto>> ExecuteAsync(Func<Task<MovementDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
