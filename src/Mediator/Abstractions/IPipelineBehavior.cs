namespace Mediator;

/// <summary>
/// Represents an async continuation for the next task to execute in the pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response.</typeparam>
/// <returns>A task representing the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Pipeline behavior to surround the inner handler.
/// Implementations add additional behavior and can optionally short-circuit the pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler. Perform any additional behavior and call next() to continue the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The delegate representing the next action in the pipeline. Awaiting this will call the next behavior or the handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the response.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
