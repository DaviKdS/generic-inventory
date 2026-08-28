using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using GenericInventory.Data;

namespace GenericInventory.Tests.Data;

public class DatabaseTransferServiceTests
{
    [Fact]
    public async Task ExportAsync_CreatesZipWithDatabaseAndManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"generic-inventory-export-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "database.db");
        await File.WriteAllBytesAsync(databasePath, CreateDatabase());

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:ConnectionString"] = $"Data Source={databasePath}",
                    ["Auth:StorePath"] = Path.Combine(root, "auth-users.json")
                })
                .Build();
            var environment = new TestWebHostEnvironment(root);
            var service = new DatabaseTransferService(configuration, environment, NullLogger<DatabaseTransferService>.Instance);

            var backup = await service.ExportAsync(CancellationToken.None);

            using var archive = new ZipArchive(new MemoryStream(backup), ZipArchiveMode.Read);
            Assert.NotNull(archive.GetEntry("database.db"));
            Assert.NotNull(archive.GetEntry("manifest.json"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] CreateDatabase()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO '" + Path.GetTempFileName().Replace("'", "''") + "'";
        var path = command.CommandText.Split('\'')[1];
        command.ExecuteNonQuery();
        var bytes = File.ReadAllBytes(path);
        File.Delete(path);
        return bytes;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string root) => ContentRootPath = root;
        public string ApplicationName { get; set; } = "GenericInventory.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}