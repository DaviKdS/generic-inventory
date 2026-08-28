using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Entities;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Options;

namespace GenericInventory.Auth.Controllers;

/// <summary>
/// Sessao do usuario: entrar, sair, solicitar acesso e definir a propria senha.
/// A distribuicao de acessos fica no AccessController, restrita a quem tem access.manage.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserAccessService _userAccessService;
    private readonly AuthOptions _authOptions;

    public AuthController(IUserAccessService userAccessService, IOptions<AuthOptions> authOptions)
    {
        _userAccessService = userAccessService;
        _authOptions = authOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<ActionResult<AuthSessionDto>> Me(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new AuthSessionDto
            {
                IsAuthenticated = false,
                ApproverEmail = _userAccessService.ApproverEmail,
                SelfRegistrationEnabled = _userAccessService.SelfRegistrationEnabled
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _userAccessService.GetUserByIdAsync(userId, cancellationToken) ?? UserFromClaims(User);
        var canManageAccess = user.Permissions.Contains(AccessPermissions.AccessManage);
        var pendingApprovals = canManageAccess
            ? await _userAccessService.CountPendingApprovalsAsync(cancellationToken)
            : 0;

        return Ok(new AuthSessionDto
        {
            IsAuthenticated = true,
            User = user,
            Permissions = user.Permissions,
            CanManageAccess = canManageAccess,
            PendingApprovals = pendingApprovals,
            ApproverEmail = _userAccessService.ApproverEmail,
            SelfRegistrationEnabled = _userAccessService.SelfRegistrationEnabled
        });
    }

    /// <summary>
    /// Abre uma solicitacao de acesso. O cadastro nasce pendente e sem permissao;
    /// quem define o papel e o administrador.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<UserAccessDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userAccessService.RegisterAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Me), user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userAccessService.ValidateLoginAsync(request, cancellationToken);
            if (user == null)
            {
                return Unauthorized(new { message = "E-mail ou senha invalidos." });
            }

            await SignInAsync(user);

            var permissions = AccessRoleCatalog.PermissionsOf(user.Role).OrderBy(item => item).ToList();
            var canManageAccess = permissions.Contains(AccessPermissions.AccessManage);

            return Ok(new AuthSessionDto
            {
                IsAuthenticated = true,
                User = UserFromRecord(user),
                Permissions = permissions,
                CanManageAccess = canManageAccess,
                PendingApprovals = canManageAccess
                    ? await _userAccessService.CountPendingApprovalsAsync(cancellationToken)
                    : 0,
                ApproverEmail = _userAccessService.ApproverEmail,
                SelfRegistrationEnabled = _userAccessService.SelfRegistrationEnabled
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>
    /// Pede um link de definicao de senha. Responde sempre igual, exista ou nao a conta,
    /// para nao permitir descobrir e-mails cadastrados.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _userAccessService.RequestPasswordLinkAsync(request.Email, cancellationToken);
        return Accepted(new { message = "Se o e-mail estiver cadastrado, o link de senha sera enviado." });
    }

    /// <summary>Consome o token de uso unico enviado por e-mail e grava a nova senha.</summary>
    [AllowAnonymous]
    [HttpPost("password/reset/{id}")]
    public async Task<ActionResult<UserAccessDto>> ResetPassword(string id, [FromBody] PasswordResetRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userAccessService.ResetPasswordAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Decisao vinda do fluxo do Power Automate. Autenticada pelo token de uso unico do cadastro;
    /// por seguranca, nunca concede o papel de administrador.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("approvals/{id}/decision")]
    public async Task<ActionResult<UserAccessDto>> DecideWithToken(string id, [FromBody] ApprovalCallbackDto request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userAccessService.DecideWithTokenAsync(id, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task SignInAsync(UserAccessRecord user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AccessClaims.Status, user.Status),
            new(AccessClaims.Stamp, user.SecurityStamp)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _authOptions.SessionHours)),
                AllowRefresh = true
            });
    }

    private static UserAccessDto UserFromClaims(ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? AccessRoleCatalog.DefaultRole;

        return new UserAccessDto
        {
            Id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            Name = principal.Identity?.Name ?? string.Empty,
            Email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Role = role,
            RoleLabel = AccessRoleCatalog.Find(role)?.Label ?? role,
            Level = AccessRoleCatalog.LevelOf(role),
            Status = principal.FindFirstValue(AccessClaims.Status) ?? AccessStatus.Approved,
            Permissions = AccessRoleCatalog.PermissionsOf(role).OrderBy(item => item).ToList()
        };
    }

    private static UserAccessDto UserFromRecord(UserAccessRecord user)
    {
        var role = AccessRoleCatalog.Find(user.Role);

        return new UserAccessDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = role?.Name ?? user.Role,
            RoleLabel = role?.Label ?? user.Role,
            Level = role?.Level ?? 0,
            Status = user.Status,
            Origin = user.Origin,
            MustDefinePassword = user.MustDefinePassword,
            Permissions = AccessRoleCatalog.PermissionsOf(user.Role).OrderBy(item => item).ToList(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            ApprovedAt = user.ApprovedAt,
            ApprovedBy = user.ApprovedBy
        };
    }
}

