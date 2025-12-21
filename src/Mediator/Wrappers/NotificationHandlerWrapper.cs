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
/// Uses ArrayPool to avoid allocations when collecting handlers.
/// </summary>
/// <typeparam name="TNotification">The type of notification.</typeparam>
internal sealed class NotificationHandlerWrapper<TNotification> : NotificationHandlerBase
    where TNotification : INotification
{
    // Expected max handlers - can grow if needed
    private const int InitialHandlerCapacity = 8;

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
    public Task Handle(
        TNotification notification,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
        
        return PublishToHandlers(notification, handlers, cancellationToken);
    }

    /// <summary>
    /// Publishes to all handlers using parallel execution.
    /// Uses ArrayPool to minimize allocations.
    /// </summary>
    private static async Task PublishToHandlers(
        TNotification notification,
        IEnumerable<INotificationHandler<TNotification>> handlers,
        CancellationToken cancellationToken)
    {
        // Rent an array from pool to collect tasks
        var taskArray = ArrayPool<Task>.Shared.Rent(InitialHandlerCapacity);
        var taskList = new List<Task>(InitialHandlerCapacity);

        try
        {
            // Collect all handler tasks
            foreach (var handler in handlers)
            {
                taskList.Add(handler.Handle(notification, cancellationToken));
            }

            // Fast path: no handlers
            if (taskList.Count == 0)
            {
                return;
            }

            // Fast path: single handler
            if (taskList.Count == 1)
            {
                await taskList[0].ConfigureAwait(false);
                return;
            }

            // Multiple handlers: await all
            await Task.WhenAll(taskList).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<Task>.Shared.Return(taskArray);
        }
    }
}
