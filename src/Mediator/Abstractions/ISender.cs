namespace Mediator;

/// <summary>
/// Defines a sender for requests.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response from the handler.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request object to a single handler.
    /// Use this for dynamic dispatch when the request type is not known at compile time.
    /// </summary>
    /// <param name="request">The request object to send.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the response as object.</returns>
    Task<object?> Send(object request, CancellationToken cancellationToken = default);
}
