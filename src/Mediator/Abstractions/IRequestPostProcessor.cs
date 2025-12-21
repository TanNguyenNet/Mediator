namespace Mediator;

/// <summary>
/// Defines a post-processor for a request.
/// Post-processors run after the request handler has completed.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Process method executes after the handler has returned.
    /// </summary>
    /// <param name="request">The request that was processed.</param>
    /// <param name="response">The response from the handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
