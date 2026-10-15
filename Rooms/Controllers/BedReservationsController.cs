using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Services;
using GenericInventory.Rooms.Dtos;
using GenericInventory.Rooms.Services;

namespace GenericInventory.Rooms.Controllers;

[ApiController]
[Route("api/rooms")]
public class BedReservationsController : ControllerBase
{
    private readonly BedReservationsService _service;
    private readonly ScreenVisibilitySettingsService _screenVisibilitySettings;

    public BedReservationsController(
        BedReservationsService service,
        ScreenVisibilitySettingsService screenVisibilitySettings)
    {
        _service = service;
        _screenVisibilitySettings = screenVisibilitySettings;
    }

    [Authorize(Policy = AccessPermissions.StockRead)]
    [HttpGet]
    public async Task<ActionResult<BedReservationsDto>> Get(CancellationToken cancellationToken)
    {
        var data = await _service.GetAsync(cancellationToken);
        FilterHiddenFields(data, await GetHiddenFieldsAsync(cancellationToken));
        return Ok(data);
    }

    [Authorize(Policy = AccessPermissions.StockMove)]
    [HttpPost]
    public async Task<ActionResult<BedDto>> Create([FromBody] BedCreateDto form, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _service.CreateAsync(form, ActingUser, cancellationToken));
    }

    [Authorize(Policy = AccessPermissions.StockMove)]
    [HttpPut("{id}")]
    public async Task<ActionResult<BedDto>> Update(string id, [FromBody] BedUpdateDto form, CancellationToken cancellationToken)
    {
        var hiddenFields = await GetHiddenFieldsAsync(cancellationToken);
        return await ExecuteAsync(async () =>
        {
            var updated = await _service.UpdateAsync(id, form, ActingUser, hiddenFields, cancellationToken);
            FilterHiddenFields(updated, hiddenFields);
            return updated;
        });
    }

    [Authorize(Policy = AccessPermissions.StockMove)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteAsync(id, ActingUser, cancellationToken);
            return NoContent();
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

    [Authorize(Policy = AccessPermissions.AccessManage)]
    [HttpPost("reset-demo")]
    public async Task<IActionResult> ResetDemo(CancellationToken cancellationToken)
    {
        await _service.ResetDemoAsync(cancellationToken);
        return NoContent();
    }

    private string ActingUser =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "sistema";

    private string ActingRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private async Task<HashSet<string>> GetHiddenFieldsAsync(CancellationToken cancellationToken)
    {
        if (AccessRoleCatalog.IsDeveloper(ActingRole))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var settings = await _screenVisibilitySettings.GetForRoleAsync(ActingRole, cancellationToken);
        var role = AccessRoleCatalog.Normalize(ActingRole);
        return settings.HiddenFieldsByRole.TryGetValue(role, out var fields)
            ? new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void FilterHiddenFields(BedReservationsDto data, IReadOnlySet<string> hiddenFields)
    {
        foreach (var area in data.Areas)
        {
            foreach (var bed in area.Rooms)
            {
                if (hiddenFields.Contains("guestName")) bed.GuestName = string.Empty;
                if (hiddenFields.Contains("guestCode")) bed.ReservationCode = string.Empty;
                if (hiddenFields.Contains("cpf")) bed.Cpf = string.Empty;
                if (hiddenFields.Contains("phone")) bed.Phone = string.Empty;
                if (hiddenFields.Contains("emergencyContact")) bed.EmergencyContact = string.Empty;
                if (hiddenFields.Contains("emergencyPhone")) bed.EmergencyPhone = string.Empty;
                if (hiddenFields.Contains("bloodType")) bed.BloodType = string.Empty;
                if (hiddenFields.Contains("checkIn")) bed.CheckIn = string.Empty;
                if (hiddenFields.Contains("checkOut")) bed.CheckOut = string.Empty;
                if (hiddenFields.Contains("actualCheckOut")) bed.ActualCheckOut = string.Empty;
                if (hiddenFields.Contains("note")) bed.Note = string.Empty;
                if (hiddenFields.Contains("operator")) bed.Operator = string.Empty;
            }
        }

        foreach (var entry in data.History)
        {
            if (hiddenFields.Contains("guestCode")) entry.ReservationCode = string.Empty;
            if (hiddenFields.Contains("note")) entry.Note = string.Empty;
            if (hiddenFields.Contains("operator")) entry.Operator = string.Empty;
        }
    }

    private static void FilterHiddenFields(BedDto bed, IReadOnlySet<string> hiddenFields)
    {
        if (hiddenFields.Contains("guestName")) bed.GuestName = string.Empty;
        if (hiddenFields.Contains("guestCode")) bed.ReservationCode = string.Empty;
        if (hiddenFields.Contains("cpf")) bed.Cpf = string.Empty;
        if (hiddenFields.Contains("phone")) bed.Phone = string.Empty;
        if (hiddenFields.Contains("emergencyContact")) bed.EmergencyContact = string.Empty;
        if (hiddenFields.Contains("emergencyPhone")) bed.EmergencyPhone = string.Empty;
        if (hiddenFields.Contains("bloodType")) bed.BloodType = string.Empty;
        if (hiddenFields.Contains("checkIn")) bed.CheckIn = string.Empty;
        if (hiddenFields.Contains("checkOut")) bed.CheckOut = string.Empty;
        if (hiddenFields.Contains("actualCheckOut")) bed.ActualCheckOut = string.Empty;
        if (hiddenFields.Contains("note")) bed.Note = string.Empty;
        if (hiddenFields.Contains("operator")) bed.Operator = string.Empty;
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
