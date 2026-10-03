using System.Collections.Concurrent;

namespace Mediator.Tests.TestData;

// ========== NOTIFICATIONS ==========

/// <summary>
/// Simple notification.
/// </summary>
public record UserCreatedNotification(int UserId, string UserName) : INotification;

/// <summary>
/// Another notification for testing.
/// </summary>
public record OrderPlacedNotification(int OrderId) : INotification;

// ========== NOTIFICATION HANDLERS ==========

/// <summary>
/// First handler for UserCreatedNotification.
/// </summary>
public class UserCreatedHandler1 : INotificationHandler<UserCreatedNotification>
{
    public static int HandleCount { get; private set; }
    public static List<UserCreatedNotification> ReceivedNotifications { get; } = new();

    public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        HandleCount++;
        ReceivedNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        HandleCount = 0;
        ReceivedNotifications.Clear();
    }
}

/// <summary>
/// Second handler for UserCreatedNotification.
/// </summary>
public class UserCreatedHandler2 : INotificationHandler<UserCreatedNotification>
{
    public static int HandleCount { get; private set; }
    public static List<UserCreatedNotification> ReceivedNotifications { get; } = new();

    public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        HandleCount++;
        ReceivedNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        HandleCount = 0;
        ReceivedNotifications.Clear();
    }
}

/// <summary>
/// Handler for OrderPlacedNotification.
/// </summary>
public class OrderPlacedHandler : INotificationHandler<OrderPlacedNotification>
{
    public static int HandleCount { get; private set; }

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        HandleCount++;
        return Task.CompletedTask;
    }

    public static void Reset() => HandleCount = 0;
}

// ========== MULTIPLE (3+) HANDLERS ==========

/// <summary>
/// Notification with three handlers to exercise the 3+ handler publish path.
/// </summary>
public record BroadcastNotification(int Id) : INotification;

/// <summary>
/// Shared base for BroadcastNotification handlers; completes asynchronously so Task.WhenAll awaits pending tasks.
/// </summary>
public abstract class BroadcastHandlerBase : INotificationHandler<BroadcastNotification>
{
    public static ConcurrentBag<string> Calls { get; } = [];

    public async Task Handle(BroadcastNotification notification, CancellationToken cancellationToken)
    {
        await Task.Yield();
        Calls.Add(GetType().Name);
    }

    public static void Reset() => Calls.Clear();
}

public class BroadcastHandlerA : BroadcastHandlerBase;

public class BroadcastHandlerB : BroadcastHandlerBase;

public class BroadcastHandlerC : BroadcastHandlerBase;

/// <summary>
/// Notification with no handlers for testing edge cases.
/// </summary>
public record OrphanNotification(string Message) : INotification;

// ========== DOMAIN EVENT INHERITANCE TESTS ==========

/// <summary>
/// Base class for domain events to test inheritance scenarios.
/// </summary>
public abstract record DomainEventBase : INotification;

/// <summary>
/// Concrete domain event for testing inheritance with Publish.
/// </summary>
public record CreateOrderEvent(int OrderId, string CustomerName) : DomainEventBase;

/// <summary>
/// Handler for CreateOrderEvent to test inheritance scenario.
/// </summary>
public class CreateOrderEventHandler : INotificationHandler<CreateOrderEvent>
{
    public static int HandleCount { get; private set; }
    public static List<CreateOrderEvent> ReceivedEvents { get; } = new();

    public Task Handle(CreateOrderEvent notification, CancellationToken cancellationToken)
    {
        HandleCount++;
        ReceivedEvents.Add(notification);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        HandleCount = 0;
        ReceivedEvents.Clear();
    }
}
