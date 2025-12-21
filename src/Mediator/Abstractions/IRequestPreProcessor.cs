namespace Mediator;

/// <summary>
/// Defines a pre-processor for a request.
/// Pre-processors run before the request handler is invoked.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
public interface IRequestPreProcessor<in TRequest>
    where TRequest : notnull
{
    /// <summary>
    /// Process method executes before calling the handler.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Process(TRequest request, CancellationToken cancellationToken);
}
