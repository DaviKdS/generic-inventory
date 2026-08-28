using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
   public const string SchemeName = "TestAuth";

   public TestAuthHandler(
      IOptionsMonitor<AuthenticationSchemeOptions> options,
      ILoggerFactory logger,
      UrlEncoder encoder)
      : base(options, logger, encoder)
   {
   }

   protected override Task<AuthenticateResult> HandleAuthenticateAsync()
   {
      var role = Request.Headers.TryGetValue("X-Test-Role", out var headerRole)
         ? headerRole.ToString()
         : AccessRoleCatalog.Admin;

      var claims = new[]
      {
         new Claim(ClaimTypes.Name, "test-user"),
         new Claim(ClaimTypes.Email, "test-user@local"),
         new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
         new Claim(ClaimTypes.Role, AccessRoleCatalog.Normalize(role)),
         new Claim(AccessClaims.Status, AccessStatus.Approved),
         new Claim(AccessClaims.Stamp, "test-stamp")
      };

      var identity = new ClaimsIdentity(claims, SchemeName);
      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, SchemeName);

      return Task.FromResult(AuthenticateResult.Success(ticket));
   }
}
