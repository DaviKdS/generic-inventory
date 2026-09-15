using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Entities;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Options;
using GenericInventory.Auth.Services;

namespace GenericInventory.Tests.Auth;

public class DeveloperAccessTests
{
    [Fact]
    public async Task Bootstrap_ShouldRestoreDeveloperDefaultPassword()
    {
        var root = Path.Combine(Path.GetTempPath(), $"generic-inventory-auth-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var storePath = Path.Combine(root, "auth-users.json");
        var oldDeveloper = new UserAccessRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Developer",
            Email = "dev@email.com",
            PasswordHash = PasswordHashingService.Hash("senha-antiga"),
            Role = AccessRoleCatalog.Standard,
            Status = AccessStatus.Pending,
            Origin = AccessOrigin.Bootstrap,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await File.WriteAllTextAsync(
            storePath,
            JsonSerializer.Serialize(new[] { oldDeveloper }, new JsonSerializerOptions { WriteIndented = true }));

        var service = CreateService(root, storePath);
        await service.EnsureBootstrapAdminAsync();

        var login = await service.ValidateLoginAsync(new LoginRequestDto
        {
            Email = "dev@email.com",
            Password = "191220023"
        });

        Assert.NotNull(login);
        Assert.Equal(AccessRoleCatalog.Developer, login.Role);
        Assert.Equal(AccessStatus.Approved, login.Status);
        Assert.False(login.MustDefinePassword);
    }

    [Fact]
    public async Task SendPasswordLink_ShouldReturnPublicPasswordSetupUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), $"generic-inventory-auth-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var storePath = Path.Combine(root, "auth-users.json");
        var user = new UserAccessRecord
        {
            Id = "user-1",
            Name = "Usuário",
            Email = "user@email.com",
            PasswordHash = PasswordHashingService.Hash("senha-antiga"),
            Role = AccessRoleCatalog.Standard,
            Status = AccessStatus.Approved,
            Origin = AccessOrigin.Invite,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await File.WriteAllTextAsync(
            storePath,
            JsonSerializer.Serialize(new[] { user }, new JsonSerializerOptions { WriteIndented = true }));

        var service = CreateService(root, storePath);
        var response = await service.SendPasswordLinkAsync(user.Id, "dev@email.com");

        Assert.Equal(user.Id, response.User.Id);
        Assert.StartsWith("https://teste.example/?view=password&uid=user-1&token=", response.PasswordSetupUrl);
        Assert.DoesNotContain("senha-antiga", response.PasswordSetupUrl);
    }

    private static FileUserAccessService CreateService(string root, string storePath)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(root);

        var options = Options.Create(new AuthOptions
        {
            ApproverEmail = "admin@email.com",
            StorePath = storePath,
            Bootstrap = new AccessBootstrapOptions
            {
                Enabled = true,
                Name = "Administrador",
                Email = "admin@email.com",
                PasswordHash = PasswordHashingService.Hash("12345678")
            }
        });

        return new FileUserAccessService(
            options,
            Options.Create(new AppNotificationOptions { PublicBaseUrl = "https://teste.example" }),
            environment.Object,
            Mock.Of<IApprovalNotifier>(),
            NullLogger<FileUserAccessService>.Instance);
    }
}
