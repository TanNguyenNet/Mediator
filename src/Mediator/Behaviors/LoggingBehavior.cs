using Microsoft.Extensions.Logging;

namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that logs request handling.
/// Uses cached type names to avoid repeated reflection.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cached type name - computed once per generic instantiation
    private static readonly string RequestTypeName = typeof(TRequest).Name;
    private static readonly string ResponseTypeName = typeof(TResponse).Name;

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Creates a new instance of the logging behavior.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Log request start
        _logger.LogInformation(
            "Handling {RequestName} -> {ResponseName}",
            RequestTypeName,
            ResponseTypeName);

        try
        {
            var response = await next().ConfigureAwait(false);

            // Log success
            _logger.LogInformation(
                "Handled {RequestName} successfully",
                RequestTypeName);

            return response;
        }
        catch (Exception ex)
        {
            // Log error
            _logger.LogError(
                ex,
                "Error handling {RequestName}: {ErrorMessage}",
                RequestTypeName,
                ex.Message);

            throw;
        }
    }
}
