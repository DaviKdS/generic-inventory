using System.Security.Claims;
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

    [Authorize(Policy = AccessPermissions.AccessManage)]
    [HttpGet]
    public async Task<ActionResult<ScreenVisibilityDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.GetAsync(cancellationToken));
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<ActionResult<ScreenVisibilityDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var actingRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Ok(await _settingsService.GetForRoleAsync(actingRole, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.AccessManage)]
    [HttpPut]
    public async Task<ActionResult<ScreenVisibilityDto>> Save(
        [FromBody] ScreenVisibilityFormDto form,
        CancellationToken cancellationToken)
    {
        try
        {
            var actingRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Ok(await _settingsService.SaveAsync(form, actingRole, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
