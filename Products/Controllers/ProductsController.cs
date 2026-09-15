using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Products.Dtos;
using GenericInventory.Products.Services;

namespace GenericInventory.Products.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductsService _service;
    private readonly CatalogImportService _catalogImportService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        ProductsService service,
        CatalogImportService catalogImportService,
        IWebHostEnvironment environment)
    {
        _service = service;
        _catalogImportService = catalogImportService;
        _environment = environment;
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
    [HttpPost("image")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<object>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "Envie uma imagem válida." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Use uma imagem JPG, PNG, WEBP ou GIF." });
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var outputPath = Path.Combine(uploadDirectory, fileName);
        await using var stream = System.IO.File.Create(outputPath);
        await file.CopyToAsync(stream, cancellationToken);

        return Ok(new { imagePath = $"/uploads/products/{fileName}" });
    }

    [Authorize(Policy = AccessPermissions.CatalogImport)]
    [HttpPost("catalog/preview")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<CatalogImportPreviewDto>> PreviewCatalog(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _catalogImportService.PreviewAsync(file, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AccessPermissions.CatalogImport)]
    [HttpPost("catalog/import")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<CatalogImportResultDto>> ImportCatalog(
        IFormFile file,
        [FromForm] string mapping,
        CancellationToken cancellationToken)
    {
        try
        {
            var parsedMapping = JsonSerializer.Deserialize<CatalogImportMappingDto>(
                mapping,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CatalogImportMappingDto();
            return Ok(await _catalogImportService.ImportAsync(file, parsedMapping, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "Mapeamento de importação inválido." });
        }
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
