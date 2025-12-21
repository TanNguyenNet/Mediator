using System.Buffers;
using System.Runtime.CompilerServices;

namespace Mediator.Wrappers;

/// <summary>
/// Abstract base class for notification handler wrappers.
/// </summary>
internal abstract class NotificationHandlerBase
{
    /// <summary>
    /// Handles the notification by dispatching to all registered handlers.
    /// </summary>
    public abstract Task Handle(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Wrapper for notification handlers.
/// Optimized for minimal allocations using ArrayPool.
/// </summary>
/// <typeparam name="TNotification">The type of notification.</typeparam>
internal sealed class NotificationHandlerWrapper<TNotification> : NotificationHandlerBase
    where TNotification : INotification
{
    // Initial capacity for handler array
    private const int InitialCapacity = 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Task Handle(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return Handle((TNotification)notification, serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Handles the strongly-typed notification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task Handle(
        TNotification notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
        
        // Fast path: no handlers
        if (handlers.Length == 0)
        {
            return Task.CompletedTask;
        }

        // Fast path: single handler - avoid Task.WhenAll overhead
        if (handlers.Length == 1)
        {
            return handlers[0].Handle(notification, cancellationToken);
        }

        return PublishToMultipleHandlers(notification, handlers, cancellationToken);
    }

    /// <summary>
    /// Publishes to multiple handlers using ArrayPool for task collection.
    /// </summary>
    private static async Task PublishToMultipleHandlers(
        TNotification notification,
        INotificationHandler<TNotification>[] handlers,
        CancellationToken cancellationToken)
    {
        var handlerCount = handlers.Length;
        
        // Rent array from pool for tasks
        var tasks = ArrayPool<Task>.Shared.Rent(handlerCount);
        
        try
        {
            // Start all handler tasks
            for (var i = 0; i < handlerCount; i++)
            {
                tasks[i] = handlers[i].Handle(notification, cancellationToken);
            }

            // Await all tasks - create span to avoid including extra rented slots
            await Task.WhenAll(new ArraySegment<Task>(tasks, 0, handlerCount)).ConfigureAwait(false);
        }
        finally
        {
            // Clear and return to pool
            Array.Clear(tasks, 0, handlerCount);
            ArrayPool<Task>.Shared.Return(tasks);
        }
    }
}
