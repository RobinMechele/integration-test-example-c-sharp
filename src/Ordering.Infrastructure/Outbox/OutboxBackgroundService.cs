using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ordering.Infrastructure.Outbox;

/// <summary>
/// Runs <see cref="OutboxProcessor"/> on a timer. Deliberately thin (a "humble object"): all logic worth testing
/// lives in <see cref="OutboxProcessor"/>, which the tests call directly instead of waiting for this loop.
/// </summary>
internal sealed partial class OutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            LogDisabled();
            return;
        }

        using var timer = new PeriodicTimer(settings.PollingInterval, timeProvider);
        do
        {
            try
            {
                int claimed;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                    claimed = await processor.ProcessAsync(stoppingToken);
                }
                while (claimed == settings.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep the loop alive: a database hiccup must not stop publishing for good.
                LogProcessingFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The outbox background publisher is disabled.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing the outbox failed.")]
    private partial void LogProcessingFailed(Exception exception);
}
