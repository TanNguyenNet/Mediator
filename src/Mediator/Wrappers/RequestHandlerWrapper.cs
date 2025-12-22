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
    /// Used for non-generic Send(object) overload.
    /// </summary>
    public abstract Task<object?> Handle(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Interface for typed request handling - avoids boxing.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
internal interface ITypedRequestHandler<TResponse>
{
    Task<TResponse> HandleTyped(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Wrapper for request handlers that handles pipeline behaviors.
/// Uses static caching to avoid repeated reflection.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerBase, ITypedRequestHandler<TResponse>
    where TRequest : IRequest<TResponse>
{
    // Cache service types to avoid repeated typeof() calls in hot path
    private static readonly Type HandlerServiceType = typeof(IRequestHandler<TRequest, TResponse>);

    /// <summary>
    /// Typed handler - called from generic Send&lt;TResponse&gt;.
    /// Avoids boxing the response.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> HandleTyped(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return HandleCore((TRequest)request, serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Object-based handler - called from non-generic Send(object).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task<object?> Handle(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return await HandleCore((TRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core handler logic - shared between typed and object-based paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<TResponse> HandleCore(
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Get the actual handler from DI using cached type
        var handler = (IRequestHandler<TRequest, TResponse>?)serviceProvider.GetService(HandlerServiceType);

        if (handler is null)
        {
            ThrowHandlerNotFound();
        }

        // Get pipeline behaviors
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

        // Fast path: no behaviors - directly call handler (most common case)
        if (behaviors.Length == 0)
        {
            return handler!.Handle(request, cancellationToken);
        }

        // Has behaviors - build pipeline
        return ExecutePipelineWithBehaviors(request, handler!, behaviors, cancellationToken);
    }

    /// <summary>
    /// Executes the pipeline when behaviors exist.
    /// Separated to keep the fast path small and inlinable.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<TResponse> ExecutePipelineWithBehaviors(
        TRequest request,
        IRequestHandler<TRequest, TResponse> handler,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        CancellationToken cancellationToken)
    {
        // Build pipeline from innermost to outermost
        RequestHandlerDelegate<TResponse> next = () => handler.Handle(request, cancellationToken);

        // Wrap in reverse order
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var currentNext = next;
            next = () => behavior.Handle(request, currentNext, cancellationToken);
        }

        return next();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowHandlerNotFound()
    {
        throw new InvalidOperationException(
            $"No handler registered for request type {typeof(TRequest).FullName}");
    }
}

/// <summary>
/// Extension methods for IServiceProvider used internally.
/// </summary>
internal static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets all services of the specified type.
    /// Returns empty array if no services are registered.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] GetServices<T>(this IServiceProvider serviceProvider)
    {
        var services = (IEnumerable<T>?)serviceProvider.GetService(typeof(IEnumerable<T>));
        return services as T[] ?? services?.ToArray() ?? Array.Empty<T>();
    }
}
