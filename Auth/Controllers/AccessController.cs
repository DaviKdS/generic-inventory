using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenericInventory.Data;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Services;

namespace GenericInventory.Auth.Controllers;

/// <summary>
/// Painel de distribuicao de acessos. Todo o controller exige a permissao access.manage,
/// que no catalogo pertence somente ao papel de administrador.
/// </summary>
[ApiController]
[Authorize(Policy = AccessPermissions.AccessManage)]
[Route("api/access")]
public class AccessController : ControllerBase
{
    private readonly IUserAccessService _userAccessService;
    private readonly DatabaseTransferService _databaseTransferService;
    private readonly ApprovalFlowSettingsService _approvalFlowSettingsService;

    public AccessController(
        IUserAccessService userAccessService,
        DatabaseTransferService databaseTransferService,
        ApprovalFlowSettingsService approvalFlowSettingsService)
    {
        _userAccessService = userAccessService;
        _databaseTransferService = databaseTransferService;
        _approvalFlowSettingsService = approvalFlowSettingsService;
    }

    /// <summary>Hierarquia publicada: papeis, permissoes e a matriz entre eles.</summary>
    [HttpGet("catalog")]
    public ActionResult<AccessCatalogDto> GetCatalog()
    {
        return Ok(new AccessCatalogDto
        {
            Roles = AccessRoleCatalog.All.Select(role => new AccessRoleDto
            {
                Name = role.Name,
                Label = role.Label,
                Description = role.Description,
                Level = role.Level,
                Permissions = role.Permissions.OrderBy(permission => permission).ToList()
            }).ToList(),
            Permissions = AccessPermissions.All.Select(permission => new AccessPermissionDto
            {
                Name = permission,
                Label = AccessPermissions.LabelOf(permission)
            }).ToList(),
            Statuses = AccessStatus.All,
            DefaultRole = AccessRoleCatalog.DefaultRole
        });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserAccessDto>>> GetUsers([FromQuery] UserQueryDto query, CancellationToken cancellationToken)
    {
        return Ok(await _userAccessService.GetUsersAsync(query, cancellationToken));
    }

    /// <summary>Cria um acesso ja liberado e envia o convite de senha por e-mail.</summary>
    [HttpPost("users")]
    public async Task<ActionResult<UserAccessDto>> InviteUser([FromBody] InviteUserRequestDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.InviteAsync(request, ActingUser, cancellationToken));
    }

    [HttpPut("users/{id}/role")]
    public async Task<ActionResult<UserAccessDto>> ChangeRole(string id, [FromBody] ChangeRoleRequestDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.ChangeRoleAsync(id, request.Role, ActingUser, cancellationToken));
    }

    [HttpPost("users/{id}/suspend")]
    public async Task<ActionResult<UserAccessDto>> Suspend(string id, [FromBody] AccessStatusRequestDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.SuspendAsync(id, ActingUser, request.Reason, cancellationToken));
    }

    [HttpPost("users/{id}/reactivate")]
    public async Task<ActionResult<UserAccessDto>> Reactivate(string id, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.ReactivateAsync(id, ActingUser, cancellationToken));
    }

    /// <summary>Reenvia o link de definicao de senha para o usuario.</summary>
    [HttpPost("users/{id}/password-link")]
    public async Task<ActionResult<UserAccessDto>> SendPasswordLink(string id, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.SendPasswordLinkAsync(id, ActingUser, cancellationToken));
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken cancellationToken)
    {
        try
        {
            await _userAccessService.RemoveAsync(id, ActingUser, cancellationToken);
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

    [HttpGet("approvals")]
    public async Task<ActionResult<IEnumerable<UserAccessDto>>> GetApprovals(CancellationToken cancellationToken)
    {
        return Ok(await _userAccessService.GetPendingApprovalsAsync(cancellationToken));
    }

    [HttpGet("approval-flow/settings")]
    public async Task<ActionResult<ApprovalFlowSettingsDto>> GetApprovalFlowSettings(CancellationToken cancellationToken)
    {
        return Ok(await _approvalFlowSettingsService.GetAsync(cancellationToken));
    }

    [HttpPut("approval-flow/settings")]
    public async Task<ActionResult<ApprovalFlowSettingsDto>> SaveApprovalFlowSettings(
        [FromBody] ApprovalFlowSettingsFormDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _approvalFlowSettingsService.SaveAsync(request, ActingUser, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("database/export")]
    public async Task<IActionResult> ExportDatabase(CancellationToken cancellationToken)
    {
        try
        {
            var content = await _databaseTransferService.ExportAsync(cancellationToken);
            return File(content, "application/zip", $"generic-inventory-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("database/import")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> ImportDatabase(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Selecione um arquivo ZIP valido." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await _databaseTransferService.ImportAsync(stream, cancellationToken);
            return Ok(new { message = "Banco de dados importado com sucesso. Atualize a pagina." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("approvals/{id}/approve")]
    public async Task<ActionResult<UserAccessDto>> Approve(string id, [FromBody] ApprovalDecisionDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.ApproveAsync(id, request.Role, ActingUser, cancellationToken));
    }

    [HttpPost("approvals/{id}/reject")]
    public async Task<ActionResult<UserAccessDto>> Reject(string id, [FromBody] ApprovalDecisionDto request, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(() => _userAccessService.RejectAsync(id, ActingUser, request.Reason, cancellationToken));
    }

    /// <summary>E-mail de quem esta operando; e o que vai para os campos de auditoria.</summary>
    private string ActingUser =>
        User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? AccessRoleCatalog.Admin;

    /// <summary>
    /// Traduz as excecoes de dominio para HTTP: invariante violada vira 409, registro ausente vira 404.
    /// </summary>
    private async Task<ActionResult<UserAccessDto>> ExecuteAsync(Func<Task<UserAccessDto>> action)
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

