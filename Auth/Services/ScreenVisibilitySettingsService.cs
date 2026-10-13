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
    private static readonly HashSet<string> ConfigurableBedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "guestName",
        "guestCode",
        "cpf",
        "phone",
        "emergencyContact",
        "emergencyPhone",
        "bloodType",
        "checkIn",
        "checkOut",
        "actualCheckOut",
        "note",
        "operator"
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
                return new ScreenVisibilityDto
                {
                    HiddenScreensByRole = Normalize(null),
                    HiddenFieldsByRole = Normalize(null, ConfigurableBedFields)
                };
            }

            await using var stream = File.OpenRead(_storePath);
            var stored = await JsonSerializer.DeserializeAsync<ScreenVisibilityDto>(stream, JsonOptions, cancellationToken)
                ?? new ScreenVisibilityDto();
            stored.HiddenScreensByRole = Normalize(stored.HiddenScreensByRole);
            stored.HiddenFieldsByRole = Normalize(stored.HiddenFieldsByRole, ConfigurableBedFields);
            return stored;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ScreenVisibilityDto> GetForRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = AccessRoleCatalog.Normalize(role);
        var settings = await GetAsync(cancellationToken);
        return new ScreenVisibilityDto
        {
            HiddenScreensByRole = OnlyRole(settings.HiddenScreensByRole, normalizedRole),
            HiddenFieldsByRole = OnlyRole(settings.HiddenFieldsByRole, normalizedRole)
        };
    }

    public async Task<ScreenVisibilityDto> SaveAsync(
        ScreenVisibilityFormDto form,
        string actingRole,
        CancellationToken cancellationToken = default)
    {
        var writableRoles = WritableRolesFor(actingRole);
        if (writableRoles.Count == 0)
        {
            throw new UnauthorizedAccessException("Perfil sem permissão para alterar visibilidade.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken);
            var settings = new ScreenVisibilityDto
            {
                HiddenScreensByRole = MergeByWritableRole(
                    current.HiddenScreensByRole,
                    Normalize(form.HiddenScreensByRole),
                    writableRoles),
                HiddenFieldsByRole = MergeByWritableRole(
                    current.HiddenFieldsByRole,
                    Normalize(form.HiddenFieldsByRole, ConfigurableBedFields),
                    writableRoles)
            };

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

    private async Task<ScreenVisibilityDto> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return new ScreenVisibilityDto
            {
                HiddenScreensByRole = Normalize(null),
                HiddenFieldsByRole = Normalize(null, ConfigurableBedFields)
            };
        }

        await using var stream = File.OpenRead(_storePath);
        var stored = await JsonSerializer.DeserializeAsync<ScreenVisibilityDto>(stream, JsonOptions, cancellationToken)
            ?? new ScreenVisibilityDto();
        stored.HiddenScreensByRole = Normalize(stored.HiddenScreensByRole);
        stored.HiddenFieldsByRole = Normalize(stored.HiddenFieldsByRole, ConfigurableBedFields);
        return stored;
    }

    private static Dictionary<string, List<string>> Normalize(
        Dictionary<string, List<string>>? input,
        IReadOnlySet<string>? allowedValues = null)
    {
        var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in ConfigurableRoles)
        {
            var valuesForRole = input != null && input.TryGetValue(role, out var values)
                ? values
                : new List<string>();

            output[role] = valuesForRole
                .Where(screen => !string.IsNullOrWhiteSpace(screen))
                .Select(screen => screen.Trim())
                .Where(screen => allowedValues == null || allowedValues.Contains(screen))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(screen => screen)
                .ToList();
        }

        return output;
    }

    private static Dictionary<string, List<string>> MergeByWritableRole(
        Dictionary<string, List<string>> current,
        Dictionary<string, List<string>> incoming,
        IReadOnlySet<string> writableRoles)
    {
        var output = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in ConfigurableRoles)
        {
            var source = writableRoles.Contains(role) ? incoming : current;
            output[role] = source.TryGetValue(role, out var values) ? values : new List<string>();
        }

        return output;
    }

    private static IReadOnlySet<string> WritableRolesFor(string? actingRole)
    {
        if (AccessRoleCatalog.IsDeveloper(actingRole))
        {
            return ConfigurableRoles;
        }

        return string.Equals(AccessRoleCatalog.Normalize(actingRole), AccessRoleCatalog.Admin, StringComparison.OrdinalIgnoreCase)
            ? new HashSet<string>(new[] { AccessRoleCatalog.Standard }, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<string>> OnlyRole(Dictionary<string, List<string>> input, string role)
    {
        var output = Normalize(null);
        if (input.TryGetValue(role, out var values))
        {
            output[role] = values;
        }

        return output;
    }
}
