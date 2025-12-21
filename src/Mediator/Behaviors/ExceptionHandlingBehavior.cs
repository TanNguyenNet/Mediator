using Microsoft.Extensions.Logging;

namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that handles exceptions and provides consistent error handling.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cached type name
    private static readonly string RequestTypeName = typeof(TRequest).Name;

    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Creates a new instance of the exception handling behavior.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExceptionHandlingBehavior(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Request {RequestName} was cancelled",
                RequestTypeName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception in request {RequestName}: {Message}",
                RequestTypeName,
                ex.Message);

            throw;
        }
    }
}
