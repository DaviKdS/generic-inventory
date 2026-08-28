using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Options;
using GenericInventory.Auth.Services;
using GenericInventory.Data;
using GenericInventory.Data.Import;
using GenericInventory.Employees.Services;
using GenericInventory.Movements.Services;
using GenericInventory.Products.Services;
using GenericInventory.Reminders.Services;

Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(80);
    });
}

builder.Services.Configure<AuthOptions>(config.GetSection("Auth"));
builder.Services.Configure<SmtpOptions>(config.GetSection("Smtp"));
builder.Services.Configure<AppNotificationOptions>(config.GetSection("App"));
builder.Services.Configure<ApprovalFlowOptions>(config.GetSection("Approval"));
builder.Services.Configure<StockReminderOptions>(config.GetSection("Reminders"));
builder.Services.Configure<PowerAutomateReminderOptions>(config.GetSection("PowerAutomateReminders"));

var cookieName = config["Auth:CookieName"] ?? "generic-inventory.auth";

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?.FindFirstValue(AccessClaims.Stamp);
                var userAccessService = context.HttpContext.RequestServices.GetRequiredService<IUserAccessService>();

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(stamp) ||
                    !await userAccessService.IsSessionValidAsync(userId, stamp, context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

builder.Services.AddAccessControl();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var connectionString = config.GetConnectionString("Default") ?? config["Data:ConnectionString"] ?? "Data Source=App_Data/generic-inventory.db";
EnsureSqliteDirectory(connectionString, builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<IApprovalNotifier, ApprovalNotificationSender>();
builder.Services.AddSingleton<ApprovalFlowSettingsService>();
builder.Services.AddSingleton<IUserAccessService, FileUserAccessService>();
builder.Services.AddSingleton<XlsxWorkbookReader>();
builder.Services.AddSingleton<DatabaseTransferService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<ProductsService>();
builder.Services.AddScoped<EmployeesService>();
builder.Services.AddScoped<StockReminderService>();
builder.Services.AddScoped<PowerAutomateReminderSettingsService>();
builder.Services.AddScoped<MovementsService>();
builder.Services.AddScoped<SmtpStockNotificationSender>();
builder.Services.AddScoped<IStockNotificationSender, PowerAutomateStockNotificationSender>();
builder.Services.AddHttpClient();

var reminderOptions = config.GetSection("Reminders").Get<StockReminderOptions>() ?? new StockReminderOptions();
if (reminderOptions.SchedulerEnabled)
{
    builder.Services.AddHostedService<StockReminderBackgroundService>();
}

var swaggerServerList = builder.Configuration["SwaggerServersList"];

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Generic Inventory",
        Description = "Aplicacao generica para controle de estoque",
        Version = "0.1.0"
    });

    if (swaggerServerList != null)
    {
        foreach (var server in swaggerServerList.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            options.AddServer(new OpenApiServer { Url = server });
        }
    }
});

var app = builder.Build();

await app.EnsureDatabaseAsync();
app.EnsureAccessBootstrap();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
    }
});

app.UseSwagger(option =>
{
    option.RouteTemplate = "api/{documentName}/swagger.json";
});

app.UseSwaggerUI(option =>
{
    option.RoutePrefix = "api";
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void EnsureSqliteDirectory(string connectionString, string contentRoot)
{
    var marker = "Data Source=";
    var index = connectionString.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (index < 0)
    {
        return;
    }

    var value = connectionString[(index + marker.Length)..].Split(';', 2)[0].Trim();
    if (string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, ":memory:", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var path = Path.IsPathRooted(value) ? value : Path.Combine(contentRoot, value);
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

public partial class Program
{
}
