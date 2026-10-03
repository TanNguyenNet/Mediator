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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Task Handle(
        object notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        return HandleCore((TNotification)notification, serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Handles the strongly-typed notification.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task HandleCore(
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

        // Fast path: two handlers - common case, avoid array allocation
        if (handlers.Length == 2)
        {
            return Task.WhenAll(
                handlers[0].Handle(notification, cancellationToken),
                handlers[1].Handle(notification, cancellationToken));
        }

        // Multiple handlers (3+)
        return PublishToMultipleHandlers(notification, handlers, cancellationToken);
    }

    /// <summary>
    /// Publishes to multiple handlers (3+) using ArrayPool for task collection.
    /// Zero allocation when every handler completes synchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task PublishToMultipleHandlers(
        TNotification notification,
        INotificationHandler<TNotification>[] handlers,
        CancellationToken cancellationToken)
    {
        var handlerCount = handlers.Length;
        var tasks = ArrayPool<Task>.Shared.Rent(handlerCount);

        try
        {
            var allCompletedSuccessfully = true;

            for (var i = 0; i < handlerCount; i++)
            {
                var task = handlers[i].Handle(notification, cancellationToken);
                tasks[i] = task;
                allCompletedSuccessfully &= task.IsCompletedSuccessfully;
            }

            if (allCompletedSuccessfully)
            {
                return Task.CompletedTask;
            }

            // WhenAll(ReadOnlySpan) does not keep the span, so the array can be returned to the pool right after
            return Task.WhenAll(new ReadOnlySpan<Task>(tasks, 0, handlerCount));
        }
        finally
        {
            // Clear so the pool does not keep completed tasks alive
            Array.Clear(tasks, 0, handlerCount);
            ArrayPool<Task>.Shared.Return(tasks);
        }
    }
}
