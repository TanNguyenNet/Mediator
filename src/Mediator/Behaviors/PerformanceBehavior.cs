using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that measures request execution time.
/// Uses ObjectPool for Stopwatch to minimize allocations.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cached type name
    private static readonly string RequestTypeName = typeof(TRequest).Name;

    // Stopwatch pool to avoid allocations
    private static readonly ObjectPool<Stopwatch> StopwatchPool =
        new DefaultObjectPoolProvider().Create(new StopwatchPooledObjectPolicy());

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly long _thresholdMilliseconds;

    /// <summary>
    /// Creates a new instance of the performance behavior.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="thresholdMilliseconds">The threshold in milliseconds for logging warnings. Default is 500ms.</param>
    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        long thresholdMilliseconds = 500)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _thresholdMilliseconds = thresholdMilliseconds;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = StopwatchPool.Get();

        try
        {
            stopwatch.Restart();

            var response = await next().ConfigureAwait(false);

            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs > _thresholdMilliseconds)
            {
                _logger.LogWarning(
                    "Long running request: {RequestName} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                    RequestTypeName,
                    elapsedMs,
                    _thresholdMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "Request {RequestName} completed in {ElapsedMs}ms",
                    RequestTypeName,
                    elapsedMs);
            }

            return response;
        }
        finally
        {
            stopwatch.Reset();
            StopwatchPool.Return(stopwatch);
        }
    }
}

/// <summary>
/// Pooled object policy for Stopwatch instances.
/// </summary>
internal sealed class StopwatchPooledObjectPolicy : PooledObjectPolicy<Stopwatch>
{
    /// <inheritdoc />
    public override Stopwatch Create() => new();

    /// <inheritdoc />
    public override bool Return(Stopwatch obj)
    {
        obj.Reset();
        return true;
    }
}
