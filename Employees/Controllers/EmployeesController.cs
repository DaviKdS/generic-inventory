using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Employees.Dtos;
using GenericInventory.Employees.Services;

namespace GenericInventory.Employees.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeesService _service;

    public EmployeesController(EmployeesService service)
    {
        _service = service;
    }

    [Authorize(Policy = AccessPermissions.StockRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> Get([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(search, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.EmployeesManage)]
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] EmployeeFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.CreateAsync(form, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.EmployeesManage)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, [FromBody] EmployeeFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.UpdateAsync(id, form, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.EmployeesManage)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ActionResult<EmployeeDto>> ExecuteAsync(Func<Task<EmployeeDto>> action)
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
