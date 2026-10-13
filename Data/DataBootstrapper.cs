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
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Beds" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Beds" PRIMARY KEY,
                "Label" TEXT NOT NULL DEFAULT '',
                "Category" TEXT NOT NULL DEFAULT '',
                "Capacity" INTEGER NOT NULL DEFAULT 1,
                "Status" TEXT NOT NULL DEFAULT 'available',
                "GuestName" TEXT NOT NULL DEFAULT '',
                "GuestCode" TEXT NOT NULL DEFAULT '',
                "Cpf" TEXT NOT NULL DEFAULT '',
                "Phone" TEXT NOT NULL DEFAULT '',
                "EmergencyContact" TEXT NOT NULL DEFAULT '',
                "EmergencyPhone" TEXT NOT NULL DEFAULT '',
                "BloodType" TEXT NOT NULL DEFAULT '',
                "CheckIn" TEXT NOT NULL DEFAULT '',
                "CheckOut" TEXT NOT NULL DEFAULT '',
                "ActualCheckOut" TEXT NOT NULL DEFAULT '',
                "Note" TEXT NOT NULL DEFAULT '',
                "Operator" TEXT NOT NULL DEFAULT '',
                "Fixed" INTEGER NOT NULL DEFAULT 0,
                "OccupancyStartedAt" TEXT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Beds_Label" ON "Beds" ("Label");
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BedHistory" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BedHistory" PRIMARY KEY AUTOINCREMENT,
                "Date" TEXT NOT NULL,
                "BedId" TEXT NOT NULL DEFAULT '',
                "AreaName" TEXT NOT NULL DEFAULT 'Camas',
                "RoomLabel" TEXT NOT NULL DEFAULT '',
                "Status" TEXT NOT NULL DEFAULT 'available',
                "GuestCode" TEXT NOT NULL DEFAULT '',
                "Note" TEXT NOT NULL DEFAULT '',
                "Operator" TEXT NOT NULL DEFAULT ''
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_BedHistory_Date" ON "BedHistory" ("Date");
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
        await EnsureColumnAsync(
            db,
            "Beds",
            "OccupancyStartedAt",
            "\"OccupancyStartedAt\" TEXT NULL");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Beds"
            SET "OccupancyStartedAt" = COALESCE("UpdatedAt", CURRENT_TIMESTAMP)
            WHERE lower("Status") IN ('reserved', 'occupied')
              AND "OccupancyStartedAt" IS NULL;
            """);

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
