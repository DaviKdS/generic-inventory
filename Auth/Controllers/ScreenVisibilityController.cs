using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Services;

namespace GenericInventory.Auth.Controllers;

[ApiController]
[Route("api/access/screen-visibility")]
public class ScreenVisibilityController : ControllerBase
{
    private readonly ScreenVisibilitySettingsService _settingsService;

    public ScreenVisibilityController(ScreenVisibilitySettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<ScreenVisibilityDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.GetAsync(cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.CatalogImport)]
    [HttpPut]
    public async Task<ActionResult<ScreenVisibilityDto>> Save(
        [FromBody] ScreenVisibilityFormDto form,
        CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.SaveAsync(form, cancellationToken));
    }
}
