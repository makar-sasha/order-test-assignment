using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Order.Core.Files;
using Order.Core.Messaging;
using Order.Core.Orders;
using Order.Infrastructure.Files;
using Polly;
using Polly.Wrap;

namespace Order.Handler;

public class OrderFiles(
    IOrderRepository orderRepository,
    ISignal signal,
    IFileName fileName,
    HttpClient httpClient,
    IOptions<OrderFilesSettings> options,
    ILogger<OrderFiles> logger) : IHostedService
{
    private readonly int _maxDegreeOfParallelism = options.Value.MaxDegreeOfParallelism;
    private readonly int _signalTimeout = options.Value.SignalTimeoutSeconds;
    private bool _firstRun = true;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("OrderFiles service starting...");
        var signalPolicy = SignalPolicy();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_firstRun) _firstRun = false;
                else await signalPolicy.ExecuteAsync(ct =>
                    {
                        signal.Wait(TimeSpan.FromSeconds(_signalTimeout), ct);
                        return Task.CompletedTask;
                    }, cancellationToken);
                
                var unprocessedFileLinks = await orderRepository.UnprocessedFileLinks(cancellationToken);
                if (unprocessedFileLinks.Count == 0) continue;
                await ProcessFileLinks(unprocessedFileLinks, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing file links.");
            }
        }
    }

    private AsyncPolicyWrap SignalPolicy()
    {
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 2, 
                durationOfBreak: TimeSpan.FromSeconds(_signalTimeout),
                onBreak: (exception, timespan) =>
                {
                    logger.LogError(exception, 
                        "Signal circuit breaker opened. Retry after {BreakDuration}s", 
                        timespan.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset. Signal is healthy again.");
                });

        var fallbackPolicy = Policy
            .Handle<Exception>()
            .FallbackAsync(fallbackAction: async ct => await Task.Delay(TimeSpan.FromSeconds(2), ct));

        var combinedPolicy = Policy.WrapAsync(fallbackPolicy, circuitBreakerPolicy);
        return combinedPolicy;
    }

    private async Task ProcessFileLinks(IList<FileLink> fileLinks, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(fileLinks, new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        }, async (fileLink, token) =>
        {
            await ProcessFileLink(fileLink, token);
        });
    }

    private async Task ProcessFileLink(FileLink fileLink, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing file link: {@FileLink}", fileLink);

            IWebFile webFile = new WebFile(httpClient, fileLink);

            var destinationPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                nameof(OrderFiles), 
                fileLink.OrderId.ToString(), 
                fileName.Sanitize(fileLink.Brand),
                fileName.Sanitize(fileLink.Variant));
            await webFile.Save(destinationPath, cancellationToken);
            var result = webFile.Result();
            logger.LogInformation("File received: {@SaveResult}", result);

            await orderRepository.ProcessFileLink(fileLink.Id, cancellationToken);
            logger.LogInformation("File link processed successfully: {@FileLink}", fileLink);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing file link {@FileLink}.", fileLink);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("OrderFiles service stopping...");
        return Task.CompletedTask;
    }
}