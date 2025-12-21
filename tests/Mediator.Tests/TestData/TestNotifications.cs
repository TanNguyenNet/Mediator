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

/// <summary>
/// Notification with no handlers for testing edge cases.
/// </summary>
public record OrphanNotification(string Message) : INotification;
