using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace GenericInventory.Tests.Auth;

public class ScreenVisibilitySettingsServiceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"generic-inventory-visibility-{Guid.NewGuid():N}");

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Developer_ShouldSaveScreenAndFieldVisibilityForAdminAndUser()
    {
        var service = CreateService();

        var saved = await service.SaveAsync(new ScreenVisibilityFormDto
        {
            HiddenScreensByRole = new(StringComparer.OrdinalIgnoreCase)
            {
                [AccessRoleCatalog.Admin] = new List<string> { "access" },
                [AccessRoleCatalog.Standard] = new List<string> { "rooms" }
            },
            HiddenFieldsByRole = new(StringComparer.OrdinalIgnoreCase)
            {
                [AccessRoleCatalog.Admin] = new List<string> { "cpf" },
                [AccessRoleCatalog.Standard] = new List<string> { "phone", "not-a-field" }
            }
        }, AccessRoleCatalog.Developer);

        Assert.Contains("access", saved.HiddenScreensByRole[AccessRoleCatalog.Admin]);
        Assert.Contains("rooms", saved.HiddenScreensByRole[AccessRoleCatalog.Standard]);
        Assert.Contains("cpf", saved.HiddenFieldsByRole[AccessRoleCatalog.Admin]);
        Assert.Contains("phone", saved.HiddenFieldsByRole[AccessRoleCatalog.Standard]);
        Assert.DoesNotContain("not-a-field", saved.HiddenFieldsByRole[AccessRoleCatalog.Standard]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Admin_ShouldOnlySaveVisibilityForUserAndPreserveAdminConfiguration()
    {
        var service = CreateService();
        await service.SaveAsync(new ScreenVisibilityFormDto
        {
            HiddenFieldsByRole = new(StringComparer.OrdinalIgnoreCase)
            {
                [AccessRoleCatalog.Admin] = new List<string> { "cpf" },
                [AccessRoleCatalog.Standard] = new List<string> { "phone" }
            }
        }, AccessRoleCatalog.Developer);

        var saved = await service.SaveAsync(new ScreenVisibilityFormDto
        {
            HiddenFieldsByRole = new(StringComparer.OrdinalIgnoreCase)
            {
                [AccessRoleCatalog.Admin] = new List<string> { "phone" },
                [AccessRoleCatalog.Standard] = new List<string> { "cpf" }
            }
        }, AccessRoleCatalog.Admin);

        Assert.Contains("cpf", saved.HiddenFieldsByRole[AccessRoleCatalog.Admin]);
        Assert.DoesNotContain("phone", saved.HiddenFieldsByRole[AccessRoleCatalog.Admin]);
        Assert.Contains("cpf", saved.HiddenFieldsByRole[AccessRoleCatalog.Standard]);
        Assert.DoesNotContain("phone", saved.HiddenFieldsByRole[AccessRoleCatalog.Standard]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private ScreenVisibilitySettingsService CreateService()
    {
        Directory.CreateDirectory(_contentRoot);
        return new ScreenVisibilitySettingsService(new FakeWebHostEnvironment(_contentRoot));
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootFileProvider = ContentRootFileProvider;
        }

        public string ApplicationName { get; set; } = "GenericInventory.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Testing";

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }
    }
}
