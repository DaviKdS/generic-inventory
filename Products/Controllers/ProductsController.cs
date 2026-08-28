using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Products.Dtos;
using GenericInventory.Products.Services;

namespace GenericInventory.Products.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductsService _service;

    public ProductsController(ProductsService service)
    {
        _service = service;
    }

    [Authorize(Policy = AccessPermissions.StockRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] bool criticalOnly,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(search, criticalOnly, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.ProductsManage)]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] ProductFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.CreateAsync(form, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.ProductsManage)]
    [HttpPut("{code}")]
    public async Task<ActionResult<ProductDto>> Update(string code, [FromBody] ProductFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.UpdateAsync(code, form, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.ProductsManage)]
    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteAsync(code, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ActionResult<ProductDto>> ExecuteAsync(Func<Task<ProductDto>> action)
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
