using Microsoft.Extensions.Options;

namespace GenericInventory.Reminders.Services;

public class StockReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StockReminderOptions _options;
    private readonly ILogger<StockReminderBackgroundService> _logger;

    public StockReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<StockReminderOptions> options,
        ILogger<StockReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(10, _options.PollSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<StockReminderService>();
                await service.RunDueDailyRulesAsync(DateTimeOffset.Now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao processar lembretes de estoque.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
