using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Reminders.Dtos;
using GenericInventory.Reminders.Services;

namespace GenericInventory.Reminders.Controllers;

[ApiController]
[Authorize(Policy = AccessPermissions.RemindersManage)]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly StockReminderService _service;
    private readonly PowerAutomateReminderSettingsService _powerAutomateSettingsService;

    public RemindersController(
        StockReminderService service,
        PowerAutomateReminderSettingsService powerAutomateSettingsService)
    {
        _service = service;
        _powerAutomateSettingsService = powerAutomateSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReminderRuleDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(cancellationToken));
    }

    [HttpGet("delivery-status")]
    public async Task<ActionResult<ReminderDeliveryStatusDto>> GetDeliveryStatus(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetDeliveryStatusAsync(cancellationToken));
    }

    [HttpGet("power-automate/settings")]
    public async Task<ActionResult<PowerAutomateReminderSettingsDto>> GetPowerAutomateSettings(CancellationToken cancellationToken)
    {
        return Ok(await _powerAutomateSettingsService.GetAsync(cancellationToken));
    }

    [HttpPut("power-automate/settings")]
    public async Task<ActionResult<PowerAutomateReminderSettingsDto>> SavePowerAutomateSettings(
        [FromBody] PowerAutomateReminderSettingsFormDto form,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _powerAutomateSettingsService.SaveAsync(form, ActingUser, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ReminderRuleDto>> Create([FromBody] ReminderRuleFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteRuleAsync(() => _service.CreateAsync(form, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReminderRuleDto>> Update(int id, [FromBody] ReminderRuleFormDto form, CancellationToken cancellationToken)
    {
        return await ExecuteRuleAsync(() => _service.UpdateAsync(id, form, cancellationToken));
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<ActionResult<ReminderRuleDto>> Duplicate(int id, CancellationToken cancellationToken)
    {
        return await ExecuteRuleAsync(() => _service.DuplicateAsync(id, cancellationToken));
    }

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

    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<ReminderSendResultDto>> Test(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.SendRuleAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<ActionResult<ReminderRuleDto>> ExecuteRuleAsync(Func<Task<ReminderRuleDto>> action)
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

    private string ActingUser =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? AccessRoleCatalog.Admin;
}
