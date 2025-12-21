using System.Runtime.CompilerServices;

namespace Mediator.Wrappers;

/// <summary>
/// Abstract base class for request handler wrappers.
/// This enables storing different generic handler wrappers in a single cache.
/// </summary>
internal abstract class RequestHandlerBase
{
    /// <summary>
    /// Handles the request and returns the response as Task of object.
    /// </summary>
    public abstract Task<object?> Handle(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Wrapper for request handlers that handles pipeline behaviors.
/// Uses static caching to avoid repeated reflection.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerBase
    where TRequest : IRequest<TResponse>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task<object?> Handle(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return await Handle((TRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the strongly-typed request.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> Handle(
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Get the actual handler from DI
        var handler = (IRequestHandler<TRequest, TResponse>?)serviceProvider.GetService(
            typeof(IRequestHandler<TRequest, TResponse>));

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type {typeof(TRequest).FullName}");
        }

        // Get pipeline behaviors
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        // Build the pipeline
        return ExecutePipeline(request, handler, behaviors, cancellationToken);
    }

    /// <summary>
    /// Executes the pipeline of behaviors and the handler.
    /// </summary>
    private static Task<TResponse> ExecutePipeline(
        TRequest request,
        IRequestHandler<TRequest, TResponse> handler,
        IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors,
        CancellationToken cancellationToken)
    {
        // Start with the innermost handler
        RequestHandlerDelegate<TResponse> handlerDelegate = () => handler.Handle(request, cancellationToken);

        // Wrap with behaviors in reverse order (so first registered behavior executes first)
        foreach (var behavior in behaviors.Reverse())
        {
            var next = handlerDelegate;
            var currentBehavior = behavior;
            handlerDelegate = () => currentBehavior.Handle(request, next, cancellationToken);
        }

        // Execute the pipeline
        return handlerDelegate();
    }
}

/// <summary>
/// Extension methods for IServiceProvider used internally.
/// </summary>
internal static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets all services of the specified type.
    /// Returns empty enumerable if no services are registered.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> GetServices<T>(this IServiceProvider serviceProvider)
    {
        return (IEnumerable<T>?)serviceProvider.GetService(typeof(IEnumerable<T>)) 
               ?? Enumerable.Empty<T>();
    }
}
