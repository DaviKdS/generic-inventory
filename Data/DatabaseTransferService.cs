using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GenericInventory.Data;

public sealed class DatabaseTransferService
{
    private const string DatabaseEntryName = "database.db";
    private const string AuthEntryName = "auth-users.json";
    private const string ScreenVisibilityEntryName = "screen-visibility.json";
    private const string UploadsEntryPrefix = "uploads/";
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DatabaseTransferService> _logger;

    public DatabaseTransferService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<DatabaseTransferService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        var databasePath = GetDatabasePath();
        var authPath = GetAuthPath();
        var screenVisibilityPath = GetScreenVisibilityPath();
        var uploadsPath = GetUploadsPath();
        if (!File.Exists(databasePath))
        {
            throw new InvalidOperationException("O banco de dados ainda não foi criado.");
        }

        var temporaryDatabasePath = Path.Combine(Path.GetTempPath(), $"generic-inventory-export-{Guid.NewGuid():N}.db");
        try
        {
            await SnapshotDatabaseAsync(databasePath, temporaryDatabasePath, cancellationToken);
            await using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                await AddFileAsync(archive, DatabaseEntryName, temporaryDatabasePath, cancellationToken);
                if (File.Exists(authPath))
                {
                    await AddFileAsync(archive, AuthEntryName, authPath, cancellationToken);
                }

                if (File.Exists(screenVisibilityPath))
                {
                    await AddFileAsync(archive, ScreenVisibilityEntryName, screenVisibilityPath, cancellationToken);
                }

                if (Directory.Exists(uploadsPath))
                {
                    await AddDirectoryAsync(archive, uploadsPath, UploadsEntryPrefix, cancellationToken);
                }

                var manifest = archive.CreateEntry("manifest.json");
                await using var manifestStream = manifest.Open();
                await JsonSerializer.SerializeAsync(manifestStream, new
                {
                    format = "generic-inventory-database",
                    version = 1,
                    createdAtUtc = DateTimeOffset.UtcNow
                }, cancellationToken: cancellationToken);
            }

            return output.ToArray();
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Falha ao exportar o banco SQLite em {DatabasePath}.", databasePath);
            throw new InvalidOperationException("Não foi possível criar o snapshot do banco de dados.", ex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(temporaryDatabasePath);
        }
    }

    public async Task ImportAsync(Stream zipStream, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        var databaseEntry = archive.GetEntry(DatabaseEntryName)
            ?? throw new InvalidOperationException("O ZIP não contém database.db.");
        if (databaseEntry.Length == 0)
        {
            throw new InvalidOperationException("O arquivo database.db está vazio.");
        }

        var temporaryDatabasePath = Path.Combine(Path.GetTempPath(), $"generic-inventory-import-{Guid.NewGuid():N}.db");
        try
        {
            await using (var databaseFile = File.Create(temporaryDatabasePath))
            await using (var entryStream = databaseEntry.Open())
            {
                await entryStream.CopyToAsync(databaseFile, cancellationToken);
            }

            await ValidateDatabaseAsync(temporaryDatabasePath, cancellationToken);
            await RestoreDatabaseAsync(temporaryDatabasePath, cancellationToken);

            var authEntry = archive.GetEntry(AuthEntryName);
            if (authEntry != null)
            {
                var authPath = GetAuthPath();
                Directory.CreateDirectory(Path.GetDirectoryName(authPath)!);
                await using var authFile = File.Create(authPath);
                await using var authStream = authEntry.Open();
                await authStream.CopyToAsync(authFile, cancellationToken);
            }

            var screenVisibilityEntry = archive.GetEntry(ScreenVisibilityEntryName);
            if (screenVisibilityEntry != null)
            {
                var screenVisibilityPath = GetScreenVisibilityPath();
                Directory.CreateDirectory(Path.GetDirectoryName(screenVisibilityPath)!);
                await using var visibilityFile = File.Create(screenVisibilityPath);
                await using var visibilityStream = screenVisibilityEntry.Open();
                await visibilityStream.CopyToAsync(visibilityFile, cancellationToken);
            }

            await RestoreUploadsAsync(archive, cancellationToken);
        }
        finally
        {
            File.Delete(temporaryDatabasePath);
        }
    }

    private async Task RestoreDatabaseAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var targetPath = GetDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly");
        await using var target = new SqliteConnection($"Data Source={targetPath}");
        await source.OpenAsync(cancellationToken);
        await target.OpenAsync(cancellationToken);
        source.BackupDatabase(target);
    }

    private static async Task SnapshotDatabaseAsync(string sourcePath, string snapshotPath, CancellationToken cancellationToken)
    {
        SqliteConnection.ClearAllPools();
        await using (var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;Cache=Shared"))
        await using (var snapshot = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadWriteCreate"))
        {
            await source.OpenAsync(cancellationToken);
            await snapshot.OpenAsync(cancellationToken);
            source.BackupDatabase(snapshot);
        }

        SqliteConnection.ClearAllPools();
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O banco de dados do ZIP está corrompido.");
        }
    }

    private string GetDatabasePath()
    {
        var connectionString = _configuration.GetConnectionString("Default")
            ?? _configuration["Data:ConnectionString"]
            ?? "Data Source=App_Data/generic-inventory.db";
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return ResolvePath(builder.DataSource);
    }

    private string GetAuthPath()
    {
        var configuredPath = _configuration["Auth:StorePath"] ?? "App_Data/auth-users.json";
        return ResolvePath(configuredPath);
    }

    private string GetScreenVisibilityPath()
    {
        return ResolvePath("App_Data/screen-visibility.json");
    }

    private string GetUploadsPath()
    {
        return Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads");
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(_environment.ContentRootPath, path);
    }

    private static async Task AddFileAsync(ZipArchive archive, string entryName, string path, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var target = entry.Open();
        await using var source = File.OpenRead(path);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task AddDirectoryAsync(
        ZipArchive archive,
        string directory,
        string entryPrefix,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            await AddFileAsync(archive, $"{entryPrefix}{relative}", file, cancellationToken);
        }
    }

    private async Task RestoreUploadsAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var uploadsPath = GetUploadsPath();
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(UploadsEntryPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName[UploadsEntryPrefix.Length..];
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                continue;
            }

            var outputPath = Path.GetFullPath(Path.Combine(uploadsPath, relative.Replace('/', Path.DirectorySeparatorChar)));
            var rootPath = Path.GetFullPath(uploadsPath);
            if (!outputPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await using var output = File.Create(outputPath);
            await using var input = entry.Open();
            await input.CopyToAsync(output, cancellationToken);
        }
    }
}
