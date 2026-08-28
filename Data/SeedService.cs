using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.Options;
using GenericInventory.Data.Import;
using GenericInventory.Employees.Entities;
using GenericInventory.Movements.Entities;
using GenericInventory.Products.Entities;
using GenericInventory.Reminders.Entities;

namespace GenericInventory.Data;

public class SeedService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly AuthOptions _authOptions;
    private readonly XlsxWorkbookReader _reader;
    private readonly ILogger<SeedService> _logger;

    public SeedService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IOptions<AuthOptions> authOptions,
        XlsxWorkbookReader reader,
        ILogger<SeedService> logger)
    {
        _db = db;
        _environment = environment;
        _configuration = configuration;
        _authOptions = authOptions.Value;
        _reader = reader;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultReminderAsync(cancellationToken);

        if (!_configuration.GetValue("Data:SeedEnabled", true))
        {
            return;
        }

        var needsProducts = !await _db.Products.AnyAsync(cancellationToken);
        var needsMovements = !await _db.Movements.AnyAsync(cancellationToken);
        var needsEmployees = !await _db.Employees.AnyAsync(cancellationToken);
        if (!needsProducts && !needsMovements && !needsEmployees)
        {
            return;
        }

        var zipPath = ResolveSeedZipPath();
        if (!File.Exists(zipPath))
        {
            _logger.LogWarning("Seed ZIP not found at {ZipPath}. Database was created without imported data.", zipPath);
            return;
        }

        using var archive = ZipFile.OpenRead(zipPath);
        var imageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var dataWorkbook = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith("inventory-seed.xlsx", StringComparison.OrdinalIgnoreCase));
        var employeesWorkbook = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.EndsWith("Funcionarios.xlsx", StringComparison.OrdinalIgnoreCase));

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (dataWorkbook != null)
        {
            var workbookBytes = await ReadEntryAsync(dataWorkbook, cancellationToken);
            if (needsProducts)
            {
                await SeedProductsAsync(workbookBytes, imageMap, cancellationToken);
            }

            if (needsMovements)
            {
                await SeedMovementsAsync(workbookBytes, cancellationToken);
            }
        }

        if (employeesWorkbook != null && needsEmployees)
        {
            var workbookBytes = await ReadEntryAsync(employeesWorkbook, cancellationToken);
            await SeedEmployeesAsync(workbookBytes, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedProductsAsync(
        byte[] workbookBytes,
        IReadOnlyDictionary<string, string> imageMap,
        CancellationToken cancellationToken)
    {
        var rows = _reader.ReadRows(workbookBytes, "CadProdutos");
        var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = row.Get("CodigoProduto");
            if (string.IsNullOrWhiteSpace(code) || !code.Any(char.IsDigit) || !importedCodes.Add(code))
            {
                continue;
            }

            _db.Products.Add(new Product
            {
                Code = code,
                Description = row.Get("DescricaoProduto"),
                CurrentStock = ParseDecimal(row.Get("EstoqueAtual")),
                MinimumStock = ParseDecimal(row.Get("EstoqueMinimo")),
                SaleValue = ParseDecimal(row.Get("ValorVenda")),
                LegacyImageUrl = string.Empty,
                ImagePath = string.Empty,
                Catalyst = row.Get("Catalisador"),
                LegacyImportId = row.Get("__ImportId__")
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedMovementsAsync(byte[] workbookBytes, CancellationToken cancellationToken)
    {
        var products = await _db.Products.ToDictionaryAsync(product => product.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var rows = _reader.ReadRows(workbookBytes, "Movimentos");
        foreach (var row in rows)
        {
            var code = row.Get("CodigoProduto");
            var type = NormalizeMovementType(row.Get("TipoMovimento"));
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            products.TryGetValue(code, out var product);
            var quantity = ParseDecimal(row.Get("Quantidade"));
            var unitValue = ParseDecimal(row.Get("ValorUnit"));
            var totalValue = ParseDecimal(row.Get("ValorTotal"));

            _db.Movements.Add(new Movement
            {
                Date = ParseDate(row.Get("DataMovimento")),
                Type = type,
                ProductId = product?.Id,
                ProductCode = code,
                ProductDescription = row.Get("DescricaoProduto"),
                Quantity = quantity,
                UnitValue = unitValue,
                TotalValue = totalValue == 0 && unitValue != 0 ? unitValue * quantity : totalValue,
                EmployeeName = row.Get("NOME"),
                EmployeeSection = row.Get("SEÇÃO", "SECAO", "SE��O"),
                EmployeeRegistration = row.Get("MATRÍCULA", "MATRICULA", "MATR�CULA"),
                Catalyst = row.Get("Catalisador"),
                LegacyImportId = row.Get("__ImportId__")
            });
        }
    }

    private Task SeedEmployeesAsync(byte[] workbookBytes, CancellationToken cancellationToken)
    {
        var rows = _reader.ReadRows(workbookBytes, "Funcionarios");
        foreach (var row in rows)
        {
            var name = row.Get("NOME");
            var registration = row.Get("MATRÍCULA", "MATRICULA", "MATR�CULA");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(registration))
            {
                continue;
            }

            _db.Employees.Add(new Employee
            {
                Name = name,
                Registration = registration,
                Section = row.Get("SEÇÃO", "SECAO", "SE��O"),
                LegacyImportId = row.Get("__ImportId__")
            });
        }

        return Task.CompletedTask;
    }

    private async Task EnsureDefaultReminderAsync(CancellationToken cancellationToken)
    {
        var oldDefaultRule = await _db.ReminderRules.FirstOrDefaultAsync(rule =>
            rule.Name == "Estoque minimo"
            && (rule.Subject == "Alerta de estoque minimo - Controle de Estoque"
                || rule.Subject == "Alerta de estoque - Controle de Estoque"),
            cancellationToken);
        if (oldDefaultRule != null)
        {
            oldDefaultRule.Name = "Alerta de estoque baixo";
            oldDefaultRule.Subject = "Alerta de Estoque Baixo";
            if (oldDefaultRule.MessageTemplate.Contains("Produtos com estoque critico:", StringComparison.OrdinalIgnoreCase))
            {
                oldDefaultRule.MessageTemplate = StockReminderDefaults.MessageTemplate;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (await _db.ReminderRules.AnyAsync(cancellationToken))
        {
            return;
        }

        _db.ReminderRules.Add(new ReminderRule
        {
            Name = "Alerta de estoque baixo",
            IsActive = true,
            DailyTime = "08:00",
            Recipients = _authOptions.ApproverEmail,
            Subject = "Alerta de Estoque Baixo",
            MessageTemplate = StockReminderDefaults.MessageTemplate,
            UseProductMinimum = true,
            ProductCodesCsv = string.Empty,
            TriggerOnMovement = true
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private string ResolveSeedZipPath()
    {
        var configured = _configuration["Data:SeedZipPath"] ?? string.Empty;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var input = entry.Open();
        using var output = new MemoryStream();
        await input.CopyToAsync(output, cancellationToken);
        return output.ToArray();
    }

    private static decimal ParseDecimal(string value)
    {
        value = value.Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var ptValue))
        {
            return ptValue;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
            ? invariantValue
            : 0;
    }

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.Today;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 1)
        {
            return DateTime.FromOADate(serial).Date;
        }

        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed.Date;
        }

        return DateTime.Today;
    }

    private static string NormalizeMovementType(string value)
    {
        var normalized = SpreadsheetRow.Normalize(value);
        if (normalized.StartsWith("ENTRADA", StringComparison.OrdinalIgnoreCase))
        {
            return "Entrada";
        }

        if (normalized.StartsWith("SAIDA", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("SADA", StringComparison.OrdinalIgnoreCase))
        {
            return "Saida";
        }

        return value.Trim();
    }

}
