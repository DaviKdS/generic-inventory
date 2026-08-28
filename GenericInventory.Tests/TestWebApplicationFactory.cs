using Microsoft.AspNetCore.Mvc.Testing;using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;using Microsoft.AspNetCore.Authentication;
using GenericInventory.Data;

namespace GenericInventory.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
   private readonly SqliteConnection _connection = new("Data Source=:memory:");

   protected override void ConfigureWebHost(IWebHostBuilder builder)
   {
      _connection.Open();
      builder.UseEnvironment("Testing");
      builder.ConfigureAppConfiguration((_, configuration) =>
      {
         configuration.AddInMemoryCollection(new Dictionary<string, string?>
         {
            ["Data:SeedEnabled"] = "false",
            ["Reminders:SchedulerEnabled"] = "false",
            ["Auth:Bootstrap:Enabled"] = "false",
            ["Auth:StorePath"] = Path.Combine(Path.GetTempPath(), $"generic-inventory-auth-{Guid.NewGuid():N}.json")
         });
      });

      builder.ConfigureServices(services =>
      {
         services.RemoveAll<DbContextOptions<AppDbContext>>();
         services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
      });

      builder.ConfigureTestServices(services =>
      {
         services.AddAuthentication(options =>
         {
            options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
         })
         .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
            TestAuthHandler.SchemeName, _ => { });
      });
   }

   protected override void Dispose(bool disposing)
   {
      base.Dispose(disposing);
      _connection.Dispose();
   }
}
