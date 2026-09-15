using System.Text.Json;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;

namespace GenericInventory.Auth.Services;

public class ScreenVisibilitySettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> ConfigurableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AccessRoleCatalog.Admin,
        AccessRoleCatalog.Standard
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _storePath;

    public ScreenVisibilitySettingsService(IWebHostEnvironment environment)
    {
        _storePath = Path.Combine(environment.ContentRootPath, "App_Data", "screen-visibility.json");
    }

    public async Task<ScreenVisibilityDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_storePath))
            {
                return new ScreenVisibilityDto();
            }

            await using var stream = File.OpenRead(_storePath);
            var stored = await JsonSerializer.DeserializeAsync<ScreenVisibilityDto>(stream, JsonOptions, cancellationToken)
                ?? new ScreenVisibilityDto();
            stored.HiddenScreensByRole = Normalize(stored.HiddenScreensByRole);
            return stored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ScreenVisibilityDto> SaveAsync(ScreenVisibilityFormDto form, CancellationToken cancellationToken = default)
    {
        var settings = new ScreenVisibilityDto
        {
            HiddenScreensByRole = Normalize(form.HiddenScreensByRole)
        };

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            await using var stream = File.Create(_storePath);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            return settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Dictionary<string, List<string>> Normalize(Dictionary<string, List<string>>? input)
    {
        var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in ConfigurableRoles)
        {
            var screens = input != null && input.TryGetValue(role, out var values)
                ? values
                : new List<string>();

            output[role] = screens
                .Where(screen => !string.IsNullOrWhiteSpace(screen))
                .Select(screen => screen.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(screen => screen)
                .ToList();
        }

        return output;
    }
}
