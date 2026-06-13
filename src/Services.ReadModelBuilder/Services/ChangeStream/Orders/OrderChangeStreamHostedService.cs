using Services.ReadModelBuilder.Domain;

namespace Services.ReadModelBuilder.Services.ChangeStream.Orders;

public class OrderChangeStreamHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderChangeStreamHostedService> _logger;

    public OrderChangeStreamHostedService(
        IServiceProvider serviceProvider,
        ILogger<OrderChangeStreamHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryCount = 0;
        const int maxRetryDelay = 30000;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Resolve change stream service per attempt so transient/scoped deps get a fresh scope.
            using var scope = _serviceProvider.CreateScope();
            var changeStreamService = scope.ServiceProvider.GetRequiredService<IChangeStreamService<Order>>();

            try
            {
                _logger.LogInformation("Starting OrderChangeStreamHostedService (attempt {RetryCount}).", retryCount + 1);
                await changeStreamService.StartAsync(stoppingToken);
                _logger.LogInformation("OrderChangeStreamHostedService started successfully.");
                retryCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("OrderChangeStreamHostedService was canceled during shutdown.");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                var delay = Math.Min(1000 * (int)Math.Pow(2, Math.Min(retryCount - 1, 5)), maxRetryDelay);

                _logger.LogError(ex, "OrderChangeStreamHostedService failed (attempt {RetryCount}). Retrying in {Delay}ms.", retryCount, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                try
                {
                    await changeStreamService.StopAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping change stream service during retry loop.");
                }
            }
        }

        _logger.LogInformation("OrderChangeStreamHostedService background execution completed.");
    }
}
