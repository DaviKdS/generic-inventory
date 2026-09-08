using Microsoft.EntityFrameworkCore;

namespace GenericInventory.Data;

public static class DataBootstrapper
{
    public static async Task EnsureDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PowerAutomateReminderSettings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PowerAutomateReminderSettings" PRIMARY KEY AUTOINCREMENT,
                "DailyWebhookUrl" TEXT NOT NULL DEFAULT '',
                "MovementWebhookUrl" TEXT NOT NULL DEFAULT '',
                "ManualWebhookUrl" TEXT NOT NULL DEFAULT '',
                "SharedSecret" TEXT NOT NULL DEFAULT '',
                "UpdatedAt" TEXT NULL,
                "UpdatedBy" TEXT NOT NULL DEFAULT ''
            );
            """);
        await EnsureColumnAsync(
            db,
            "ReminderRules",
            "IncludeProductImages",
            "\"IncludeProductImages\" INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(
            db,
            "ReminderRules",
            "MaxPhotoAttachments",
            "\"MaxPhotoAttachments\" INTEGER NOT NULL DEFAULT 3");
        await EnsureColumnAsync(
            db,
            "Products",
            "CustomFieldsJson",
            "\"CustomFieldsJson\" TEXT NOT NULL DEFAULT ''");

        var seeder = scope.ServiceProvider.GetRequiredService<SeedService>();
        await seeder.SeedAsync();
    }

    private static async Task EnsureColumnAsync(
        AppDbContext db,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        var columnExists = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (columnExists)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync();
    }
}
