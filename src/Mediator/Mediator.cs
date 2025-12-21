using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Mediator.Wrappers;

namespace Mediator;

/// <summary>
/// High-performance implementation of the Mediator pattern.
/// Uses static caching to avoid repeated reflection and minimize allocations.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Static cache for request handler wrappers.
    /// Shared across all Mediator instances for maximum efficiency.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> RequestHandlerCache = new();

    /// <summary>
    /// Static cache for notification handler wrappers.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, NotificationHandlerBase> NotificationHandlerCache = new();

    /// <summary>
    /// Creates a new instance of the Mediator.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers.</param>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null.</exception>
    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handler = RequestHandlerCache.GetOrAdd(
            requestType,
            static type => CreateRequestHandler(type, typeof(TResponse)));

        // Cast to the specific wrapper type for strongly-typed execution
        if (handler is RequestHandlerWrapper<IRequest<TResponse>, TResponse> typedHandler)
        {
            return typedHandler.Handle(request, _serviceProvider, cancellationToken);
        }

        // Fallback: use object-based handling and cast result
        return HandleRequestAsync<TResponse>(handler, request, cancellationToken);
    }

    /// <summary>
    /// Helper method to handle request and cast result.
    /// </summary>
    private async Task<TResponse> HandleRequestAsync<TResponse>(
        RequestHandlerBase handler,
        object request,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, _serviceProvider, cancellationToken).ConfigureAwait(false);
        return (TResponse)result!;
    }

    /// <inheritdoc />
    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();

        // Find the IRequest<TResponse> interface to get response type
        var requestInterfaceType = requestType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

        if (requestInterfaceType is null)
        {
            throw new ArgumentException(
                $"Request type {requestType.FullName} does not implement IRequest<TResponse>",
                nameof(request));
        }

        var responseType = requestInterfaceType.GetGenericArguments()[0];

        var handler = RequestHandlerCache.GetOrAdd(
            requestType,
            type => CreateRequestHandler(type, responseType));

        return await handler.Handle(request, _serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();
        var handler = NotificationHandlerCache.GetOrAdd(
            notificationType,
            static type => CreateNotificationHandler(type));

        return handler.Handle(notification, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();

        if (!typeof(INotification).IsAssignableFrom(notificationType))
        {
            throw new ArgumentException(
                $"Notification type {notificationType.FullName} does not implement INotification",
                nameof(notification));
        }

        var handler = NotificationHandlerCache.GetOrAdd(
            notificationType,
            static type => CreateNotificationHandler(type));

        return handler.Handle(notification, _serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Creates a request handler wrapper for the given request and response types.
    /// This method is only called once per request type due to caching.
    /// </summary>
    private static RequestHandlerBase CreateRequestHandler(Type requestType, Type responseType)
    {
        var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
        return (RequestHandlerBase)Activator.CreateInstance(wrapperType)!;
    }

    /// <summary>
    /// Creates a notification handler wrapper for the given notification type.
    /// This method is only called once per notification type due to caching.
    /// </summary>
    private static NotificationHandlerBase CreateNotificationHandler(Type notificationType)
    {
        var wrapperType = typeof(NotificationHandlerWrapper<>).MakeGenericType(notificationType);
        return (NotificationHandlerBase)Activator.CreateInstance(wrapperType)!;
    }
}
