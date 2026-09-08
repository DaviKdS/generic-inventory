using Microsoft.EntityFrameworkCore;
using GenericInventory.Employees.Entities;
using GenericInventory.Movements.Entities;
using GenericInventory.Products.Entities;
using GenericInventory.Reminders.Entities;

namespace GenericInventory.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<ReminderRule> ReminderRules => Set<ReminderRule>();
    public DbSet<PowerAutomateReminderSettings> PowerAutomateReminderSettings => Set<PowerAutomateReminderSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(product => product.Code).IsUnique();
            entity.Property(product => product.Code).HasMaxLength(64);
            entity.Property(product => product.Description).HasMaxLength(512);
            entity.Property(product => product.CurrentStock).HasColumnType("decimal(18,2)");
            entity.Property(product => product.MinimumStock).HasColumnType("decimal(18,2)");
            entity.Property(product => product.SaleValue).HasColumnType("decimal(18,2)");
            entity.Property(product => product.CustomFieldsJson).HasDefaultValue("");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(employee => employee.Registration).IsUnique();
            entity.Property(employee => employee.Name).HasMaxLength(256);
            entity.Property(employee => employee.Registration).HasMaxLength(64);
            entity.Property(employee => employee.Section).HasMaxLength(256);
        });

        modelBuilder.Entity<Movement>(entity =>
        {
            entity.HasIndex(movement => movement.Date);
            entity.HasIndex(movement => movement.ProductCode);
            entity.Property(movement => movement.Type).HasMaxLength(32);
            entity.Property(movement => movement.ProductCode).HasMaxLength(64);
            entity.Property(movement => movement.ProductDescription).HasMaxLength(512);
            entity.Property(movement => movement.Quantity).HasColumnType("decimal(18,2)");
            entity.Property(movement => movement.UnitValue).HasColumnType("decimal(18,2)");
            entity.Property(movement => movement.TotalValue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ReminderRule>(entity =>
        {
            entity.Property(rule => rule.Name).HasMaxLength(160);
            entity.Property(rule => rule.DailyTime).HasMaxLength(5);
            entity.Property(rule => rule.ThresholdQuantity).HasColumnType("decimal(18,2)");
            entity.Property(rule => rule.MaxPhotoAttachments).HasDefaultValue(3);
        });

        modelBuilder.Entity<PowerAutomateReminderSettings>(entity =>
        {
            entity.Property(settings => settings.DailyWebhookUrl).HasMaxLength(4096);
            entity.Property(settings => settings.MovementWebhookUrl).HasMaxLength(4096);
            entity.Property(settings => settings.ManualWebhookUrl).HasMaxLength(4096);
            entity.Property(settings => settings.SharedSecret).HasMaxLength(512);
            entity.Property(settings => settings.UpdatedBy).HasMaxLength(256);
        });
    }
}
