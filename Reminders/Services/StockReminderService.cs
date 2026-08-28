using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.Options;
using GenericInventory.Data;
using GenericInventory.Products.Entities;
using GenericInventory.Reminders.Dtos;
using GenericInventory.Reminders.Entities;

namespace GenericInventory.Reminders.Services;

public class StockReminderService
{
    private readonly AppDbContext _db;
    private readonly IStockNotificationSender _sender;
    private readonly SmtpOptions _smtpOptions;
    private readonly PowerAutomateReminderSettingsService _powerAutomateSettingsService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StockReminderService> _logger;

    public StockReminderService(
        AppDbContext db,
        IStockNotificationSender sender,
        IOptions<SmtpOptions> smtpOptions,
        PowerAutomateReminderSettingsService powerAutomateSettingsService,
        IWebHostEnvironment environment,
        ILogger<StockReminderService> logger)
    {
        _db = db;
        _sender = sender;
        _smtpOptions = smtpOptions.Value;
        _powerAutomateSettingsService = powerAutomateSettingsService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReminderDeliveryStatusDto> GetDeliveryStatusAsync(CancellationToken cancellationToken = default)
    {
        var smtpConfigured = !string.IsNullOrWhiteSpace(_smtpOptions.Host);
        var settings = await _powerAutomateSettingsService.GetAsync(cancellationToken);
        var dailyConfigured = settings.DailyConfigured;
        var movementConfigured = settings.MovementConfigured;
        var manualConfigured = settings.ManualConfigured;
        var powerAutomateConfigured = dailyConfigured || movementConfigured || manualConfigured;
        return new ReminderDeliveryStatusDto
        {
            PowerAutomateConfigured = powerAutomateConfigured,
            PowerAutomateDailyConfigured = dailyConfigured,
            PowerAutomateMovementConfigured = movementConfigured,
            PowerAutomateManualConfigured = manualConfigured,
            SmtpConfigured = smtpConfigured,
            Channel = powerAutomateConfigured ? "power-automate" : smtpConfigured ? "smtp" : "log",
            Message = powerAutomateConfigured
                ? "Power Automate configurado para um ou mais gatilhos de alerta."
                : smtpConfigured
                ? "Envio real por SMTP ativo."
                : "SMTP nao configurado; os alertas serao registrados no log local.",
            FallbackPath = Path.Combine("App_Data", "stock-reminders.log")
        };
    }

    public async Task<IReadOnlyList<ReminderRuleDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ReminderRules
            .AsNoTracking()
            .OrderBy(rule => rule.Name)
            .Select(rule => ReminderRuleDto.FromEntity(rule))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderRuleDto> CreateAsync(ReminderRuleFormDto form, CancellationToken cancellationToken = default)
    {
        var rule = new ReminderRule();
        Apply(rule, form);
        _db.ReminderRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return ReminderRuleDto.FromEntity(rule);
    }

    public async Task<ReminderRuleDto> UpdateAsync(int id, ReminderRuleFormDto form, CancellationToken cancellationToken = default)
    {
        var rule = await FindRuleAsync(id, cancellationToken);
        Apply(rule, form);
        await _db.SaveChangesAsync(cancellationToken);
        return ReminderRuleDto.FromEntity(rule);
    }

    public async Task<ReminderRuleDto> DuplicateAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await FindRuleAsync(id, cancellationToken);
        var copy = new ReminderRule
        {
            Name = BuildDuplicateName(source.Name),
            IsActive = source.IsActive,
            DailyTime = source.DailyTime,
            Recipients = source.Recipients,
            Subject = source.Subject,
            MessageTemplate = source.MessageTemplate,
            UseProductMinimum = source.UseProductMinimum,
            ThresholdQuantity = source.ThresholdQuantity,
            ProductCodesCsv = source.ProductCodesCsv,
            TriggerOnMovement = source.TriggerOnMovement,
            IncludeProductImages = source.IncludeProductImages,
            MaxPhotoAttachments = source.MaxPhotoAttachments
        };

        _db.ReminderRules.Add(copy);
        await _db.SaveChangesAsync(cancellationToken);
        return ReminderRuleDto.FromEntity(copy);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await FindRuleAsync(id, cancellationToken);
        _db.ReminderRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReminderSendResultDto> SendRuleAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await FindRuleAsync(id, cancellationToken);
        return await SendRuleAsync(rule, ReminderTriggerTypes.Manual, updateDailyRun: false, cancellationToken);
    }

    public async Task SendMovementAlertsAsync(Product product, CancellationToken cancellationToken = default)
    {
        var rules = await _db.ReminderRules
            .Where(rule => rule.IsActive && rule.TriggerOnMovement)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (!ProductMatches(rule, product) || !IsCritical(rule, product))
            {
                continue;
            }

            await SendRuleAsync(rule, ReminderTriggerTypes.Movement, updateDailyRun: false, cancellationToken, product);
        }
    }

    public async Task RunDueDailyRulesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var rules = await _db.ReminderRules
            .Where(rule => rule.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (!IsDue(rule, now))
            {
                continue;
            }

            await SendRuleAsync(rule, ReminderTriggerTypes.Daily, updateDailyRun: true, cancellationToken);
        }
    }

    private async Task<ReminderSendResultDto> SendRuleAsync(
        ReminderRule rule,
        string triggerType,
        bool updateDailyRun,
        CancellationToken cancellationToken,
        Product? movedProduct = null)
    {
        var products = movedProduct == null
            ? await CriticalProductsAsync(rule, cancellationToken)
            : new List<Product> { movedProduct };
        if (updateDailyRun)
        {
            rule.LastDailyRunAt = DateTimeOffset.UtcNow;
        }

        if (products.Count == 0)
        {
            if (updateDailyRun)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new ReminderSendResultDto
            {
                RuleId = rule.Id,
                CriticalProducts = 0,
                Sent = false,
                Channel = triggerType,
                Message = "Nenhum produto critico encontrado."
            };
        }

        var generatedAt = DateTimeOffset.Now;
        var recipients = ParseRecipients(rule.Recipients);
        var body = BuildBody(rule, products, triggerType, generatedAt);
        var attachments = await BuildAttachmentsAsync(rule, products, cancellationToken);
        var result = await _sender.SendAsync(new StockNotificationRequest
        {
            TriggerType = triggerType,
            Rule = rule,
            Products = products,
            Recipients = recipients,
            Subject = rule.Subject,
            Body = body,
            Attachments = attachments,
            GeneratedAt = generatedAt
        }, cancellationToken);
        if (updateDailyRun)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ReminderSendResultDto
        {
            RuleId = rule.Id,
            CriticalProducts = products.Count,
            Sent = result.Delivered,
            Logged = result.Logged,
            Channel = result.Channel,
            Detail = result.Detail,
            Message = BuildSendMessage(result)
        };
    }

    private static string BuildSendMessage(StockNotificationResult result)
    {
        if (result.Delivered)
        {
            if (string.Equals(result.Channel, "power-automate", StringComparison.OrdinalIgnoreCase))
            {
                return "Alerta enviado para o Power Automate.";
            }

            return "Alerta enviado por SMTP.";
        }

        if (result.Logged)
        {
            return "SMTP nao configurado; alerta registrado no log local.";
        }

        return string.IsNullOrWhiteSpace(result.Detail)
            ? "Alerta nao enviado."
            : result.Detail;
    }

    private static bool IsWebhookConfigured(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private async Task<List<Product>> CriticalProductsAsync(ReminderRule rule, CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking();
        var includedCodes = ParseCodes(rule.ProductCodesCsv);
        if (includedCodes.Count > 0)
        {
            query = query.Where(product => includedCodes.Contains(product.Code));
        }

        var products = await query
            .OrderBy(product => product.Description)
            .ToListAsync(cancellationToken);

        return products.Where(product => IsCritical(rule, product)).ToList();
    }

    private static bool IsDue(ReminderRule rule, DateTimeOffset now)
    {
        if (!TimeSpan.TryParse(rule.DailyTime, out var dueAt))
        {
            dueAt = TimeSpan.FromHours(8);
        }

        var today = now.Date;
        if (rule.LastDailyRunAt?.ToLocalTime().Date == today)
        {
            return false;
        }

        return now.TimeOfDay >= dueAt;
    }

    private async Task<IReadOnlyList<StockNotificationAttachment>> BuildAttachmentsAsync(
        ReminderRule rule,
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Array.Empty<StockNotificationAttachment>();
    }

    private static string BuildBody(
        ReminderRule rule,
        IReadOnlyList<Product> products,
        string triggerType,
        DateTimeOffset generatedAt)
    {
        var productLines = products
            .Select(product =>
                $"- {product.Code} | {product.Description} | Atual: {product.CurrentStock:0.##} | Minimo: {product.MinimumStock:0.##}")
            .ToList();

        return (string.IsNullOrWhiteSpace(rule.MessageTemplate)
                ? StockReminderDefaults.MessageTemplate
                : rule.MessageTemplate)
            .Replace("{RuleName}", rule.Name)
            .Replace("{Products}", string.Join(Environment.NewLine, productLines))
            .Replace("{ProductCount}", products.Count.ToString())
            .Replace("{TriggerType}", triggerType)
            .Replace("{GeneratedAt}", generatedAt.ToString("dd/MM/yyyy HH:mm"));
    }

    private static bool ProductMatches(ReminderRule rule, Product product)
    {
        var codes = ParseCodes(rule.ProductCodesCsv);
        return codes.Count == 0 || codes.Contains(product.Code);
    }

    private static bool IsCritical(ReminderRule rule, Product product)
    {
        var threshold = rule.UseProductMinimum
            ? product.MinimumStock
            : rule.ThresholdQuantity ?? product.MinimumStock;
        return product.CurrentStock <= threshold;
    }

    private string BuildDuplicateName(string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Alerta de estoque baixo" : name.Trim();
        var candidate = $"{baseName} (copia)";
        var suffix = 2;
        while (_db.ReminderRules.Any(rule => rule.Name == candidate))
        {
            candidate = $"{baseName} (copia {suffix++})";
        }

        return candidate;
    }

    private static IReadOnlyList<string> ParseRecipients(string value)
    {
        return value
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ParseCodes(string value)
    {
        return value
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ReminderRule> FindRuleAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.ReminderRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Lembrete nao encontrado.");
    }

    private static void Apply(ReminderRule rule, ReminderRuleFormDto form)
    {
        if (!TimeSpan.TryParse(form.DailyTime, out _))
        {
            throw new InvalidOperationException("Horario diario invalido. Use HH:mm.");
        }

        rule.Name = string.IsNullOrWhiteSpace(form.Name) ? "Alerta de estoque baixo" : form.Name.Trim();
        rule.IsActive = form.IsActive;
        rule.DailyTime = form.DailyTime.Trim();
        rule.Recipients = form.Recipients.Trim();
        rule.Subject = string.IsNullOrWhiteSpace(form.Subject)
            ? "Alerta de Estoque Baixo"
            : form.Subject.Trim();
        rule.MessageTemplate = string.IsNullOrWhiteSpace(form.MessageTemplate)
            ? StockReminderDefaults.MessageTemplate
            : form.MessageTemplate.Trim();
        rule.UseProductMinimum = form.UseProductMinimum;
        rule.ThresholdQuantity = form.ThresholdQuantity;
        rule.ProductCodesCsv = form.ProductCodesCsv.Trim();
        rule.TriggerOnMovement = form.TriggerOnMovement;
        rule.IncludeProductImages = false;
        rule.MaxPhotoAttachments = 0;
    }
}
