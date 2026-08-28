using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GenericInventory.Auth.Dtos;
using GenericInventory.Movements.Dtos;
using GenericInventory.Products.Dtos;
using GenericInventory.Reminders.Dtos;

namespace GenericInventory.Tests.Stock;

public class StockWorkflowIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StockWorkflowIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockInAndOut_ShouldUpdateProductStockAndCreateMovements()
    {
        var code = $"P{Guid.NewGuid():N}"[..10];
        await CreateProductAsync(code, currentStock: 10, minimumStock: 1, saleValue: 5);
        var employeeId = await CreateEmployeeAsync();

        var stockOutResponse = await _client.PostAsJsonAsync("/api/movements/stock-out", new
        {
            productCode = code,
            quantity = 3,
            employeeId
        });

        Assert.Equal(HttpStatusCode.OK, stockOutResponse.StatusCode);
        var stockOut = await stockOutResponse.Content.ReadFromJsonAsync<MovementDto>();
        Assert.NotNull(stockOut);
        Assert.Equal("Saida", stockOut!.Type);
        Assert.Equal(15, stockOut.TotalValue);

        var afterOut = await FindProductAsync(code);
        Assert.Equal(7, afterOut.CurrentStock);

        var stockInResponse = await _client.PostAsJsonAsync("/api/movements/stock-in", new
        {
            productCode = code,
            quantity = 2,
            employeeId
        });

        Assert.Equal(HttpStatusCode.OK, stockInResponse.StatusCode);
        var afterIn = await FindProductAsync(code);
        Assert.Equal(9, afterIn.CurrentStock);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StockOut_ShouldBlockWhenQuantityExceedsAvailableStock()
    {
        var code = $"P{Guid.NewGuid():N}"[..10];
        await CreateProductAsync(code, currentStock: 2, minimumStock: 1, saleValue: 5);

        var response = await _client.PostAsJsonAsync("/api/movements/stock-out", new
        {
            productCode = code,
            quantity = 3
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var product = await FindProductAsync(code);
        Assert.Equal(2, product.CurrentStock);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StandardUser_ShouldNotManageProducts()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                code = $"P{Guid.NewGuid():N}"[..10],
                description = "Produto bloqueado",
                currentStock = 1,
                minimumStock = 1,
                saleValue = 1
            })
        };
        request.Headers.Add("X-Test-Role", "standard");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReminderTest_ShouldUseFallbackLogWhenSmtpIsNotConfigured()
    {
        await ClearPowerAutomateSettingsAsync();
        var code = $"P{Guid.NewGuid():N}"[..10];
        await CreateProductAsync(
            code,
            currentStock: 1,
            minimumStock: 2,
            saleValue: 5);

        var createResponse = await _client.PostAsJsonAsync("/api/reminders", new
        {
            name = "Teste critico",
            isActive = true,
            dailyTime = "08:00",
            recipients = "estoque@example.com",
            subject = "Teste estoque",
            messageTemplate = "Criticos:\n{Products}",
            useProductMinimum = true,
            productCodesCsv = code,
            triggerOnMovement = true,
            includeProductImages = false,
            maxPhotoAttachments = 0
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var rule = await createResponse.Content.ReadFromJsonAsync<ReminderRuleDto>();
        Assert.NotNull(rule);
        Assert.False(rule!.IncludeProductImages);

        var testResponse = await _client.PostAsync($"/api/reminders/{rule.Id}/test", null);
        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        var result = await testResponse.Content.ReadFromJsonAsync<ReminderSendResultDto>();
        Assert.NotNull(result);
        Assert.False(result!.Sent);
        Assert.True(result.Logged);
        Assert.Equal("log", result.Channel);
        Assert.Equal(1, result.CriticalProducts);
        var logContent = await File.ReadAllTextAsync(result.Detail);
        Assert.Contains(code, logContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReminderDeliveryStatus_ShouldShowLogFallbackWhenSmtpIsNotConfigured()
    {
        await ClearPowerAutomateSettingsAsync();
        var status = await _client.GetFromJsonAsync<ReminderDeliveryStatusDto>("/api/reminders/delivery-status");

        Assert.NotNull(status);
        Assert.False(status!.SmtpConfigured);
        Assert.Equal("log", status.Channel);
        Assert.Contains("SMTP nao configurado", status.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reminder_ShouldDuplicateItsConfiguration()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/reminders", new
        {
            name = "Alerta especifico",
            dailyTime = "09:30",
            recipients = "estoque@example.com",
            subject = "Itens criticos",
            productCodesCsv = "P001,P002",
            triggerOnMovement = true,
            useProductMinimum = true
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var original = await createResponse.Content.ReadFromJsonAsync<ReminderRuleDto>();
        Assert.NotNull(original);

        var duplicateResponse = await _client.PostAsync($"/api/reminders/{original!.Id}/duplicate", null);

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<ReminderRuleDto>();
        Assert.NotNull(duplicate);
        Assert.NotEqual(original.Id, duplicate!.Id);
        Assert.Equal("Alerta especifico (copia)", duplicate.Name);
        Assert.Equal(original.DailyTime, duplicate.DailyTime);
        Assert.Equal(original.ProductCodesCsv, duplicate.ProductCodesCsv);
        Assert.Equal(original.TriggerOnMovement, duplicate.TriggerOnMovement);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PowerAutomateSettings_ShouldSaveMaskedUrlsAndAffectDeliveryStatus()
    {
        await ClearPowerAutomateSettingsAsync();
        var dailyUrl = "https://prod-00.westeurope.logic.azure.com/workflows/daily/triggers/manual/paths/invoke?api-version=2016-10-01&sig=secret-value";

        var saveResponse = await _client.PutAsJsonAsync("/api/reminders/power-automate/settings", new
        {
            dailyWebhookUrl = dailyUrl,
            sharedSecret = "shared-secret"
        });

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var settings = await saveResponse.Content.ReadFromJsonAsync<PowerAutomateReminderSettingsDto>();
        Assert.NotNull(settings);
        Assert.True(settings!.DailyConfigured);
        Assert.True(settings.SharedSecretConfigured);
        Assert.Equal("site", settings.DailySource);
        Assert.NotEqual(dailyUrl, settings.DailyWebhookUrlPreview);
        Assert.DoesNotContain("secret-value", settings.DailyWebhookUrlPreview);
        Assert.DoesNotContain("sig=", settings.DailyWebhookUrlPreview);

        var status = await _client.GetFromJsonAsync<ReminderDeliveryStatusDto>("/api/reminders/delivery-status");
        Assert.NotNull(status);
        Assert.True(status!.PowerAutomateConfigured);
        Assert.True(status.PowerAutomateDailyConfigured);
        Assert.Equal("power-automate", status.Channel);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PowerAutomateSettings_ShouldRejectInvalidWebhookUrl()
    {
        var response = await _client.PutAsJsonAsync("/api/reminders/power-automate/settings", new
        {
            dailyWebhookUrl = "not-a-url"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApprovalFlowSettings_ShouldSaveMaskedUrlAndAllowClearing()
    {
        var webhookUrl = "https://prod-00.westeurope.logic.azure.com/workflows/access/triggers/manual/paths/invoke?api-version=2016-10-01&sig=secret-value";

        var saveResponse = await _client.PutAsJsonAsync("/api/access/approval-flow/settings", new
        {
            webhookUrl
        });

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var settings = await saveResponse.Content.ReadFromJsonAsync<ApprovalFlowSettingsDto>();
        Assert.NotNull(settings);
        Assert.True(settings!.Configured);
        Assert.Equal("site", settings.Source);
        Assert.NotEqual(webhookUrl, settings.WebhookUrlPreview);
        Assert.DoesNotContain("secret-value", settings.WebhookUrlPreview);
        Assert.DoesNotContain("sig=", settings.WebhookUrlPreview);

        var clearResponse = await _client.PutAsJsonAsync("/api/access/approval-flow/settings", new
        {
            clearWebhookUrl = true
        });

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
    }

    private async Task ClearPowerAutomateSettingsAsync()
    {
        var response = await _client.PutAsJsonAsync("/api/reminders/power-automate/settings", new
        {
            clearDailyWebhookUrl = true,
            clearMovementWebhookUrl = true,
            clearManualWebhookUrl = true,
            clearSharedSecret = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task CreateProductAsync(
        string code,
        decimal currentStock,
        decimal minimumStock,
        decimal saleValue,
        string imagePath = "")
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            code,
            description = $"Produto {code}",
            currentStock,
            minimumStock,
            saleValue,
            imagePath,
            catalyst = "x"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<int> CreateEmployeeAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/employees", new
        {
            name = "Pessoa Teste",
            registration = $"M{Guid.NewGuid():N}"[..10],
            section = "Teste"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    private async Task<ProductDto> FindProductAsync(string code)
    {
        var products = await _client.GetFromJsonAsync<List<ProductDto>>($"/api/products?search={Uri.EscapeDataString(code)}");
        var product = Assert.Single(products ?? new List<ProductDto>());
        Assert.Equal(code, product.Code);
        return product;
    }
}
