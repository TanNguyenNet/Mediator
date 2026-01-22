using FluentAssertions;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for IMediator.Publish functionality.
/// </summary>
public class PublishTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public PublishTests()
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(PublishTests).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Reset static state
        UserCreatedHandler1.Reset();
        UserCreatedHandler2.Reset();
        OrderPlacedHandler.Reset();
        CreateOrderEventHandler.Reset();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Publish_NotifiesAllHandlers()
    {
        // Arrange
        var notification = new UserCreatedNotification(1, "John");
        UserCreatedHandler1.Reset();
        UserCreatedHandler2.Reset();

        // Act
        await _mediator.Publish(notification);

        // Assert
        UserCreatedHandler1.HandleCount.Should().Be(1);
        UserCreatedHandler2.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_HandlersReceiveCorrectNotification()
    {
        // Arrange
        var notification = new UserCreatedNotification(42, "Alice");
        UserCreatedHandler1.Reset();
        UserCreatedHandler2.Reset();

        // Act
        await _mediator.Publish(notification);

        // Assert
        UserCreatedHandler1.ReceivedNotifications.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(notification);
        UserCreatedHandler2.ReceivedNotifications.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(notification);
    }

    [Fact]
    public async Task Publish_WithSingleHandler_WorksCorrectly()
    {
        // Arrange
        var notification = new OrderPlacedNotification(123);
        OrderPlacedHandler.Reset();

        // Act
        await _mediator.Publish(notification);

        // Assert
        OrderPlacedHandler.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithNullNotification_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _mediator.Publish<UserCreatedNotification>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Publish_MultipleNotifications_AllHandled()
    {
        // Arrange
        UserCreatedHandler1.Reset();
        UserCreatedHandler2.Reset();

        // Act
        for (int i = 0; i < 5; i++)
        {
            await _mediator.Publish(new UserCreatedNotification(i, $"User{i}"));
        }

        // Assert
        UserCreatedHandler1.HandleCount.Should().Be(5);
        UserCreatedHandler2.HandleCount.Should().Be(5);
        UserCreatedHandler1.ReceivedNotifications.Should().HaveCount(5);
    }

    [Fact]
    public async Task Publish_ObjectOverload_WorksCorrectly()
    {
        // Arrange
        object notification = new OrderPlacedNotification(999);
        OrderPlacedHandler.Reset();

        // Act
        await _mediator.Publish(notification);

        // Assert
        OrderPlacedHandler.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_ConcurrentNotifications_AllHandled()
    {
        // Arrange
        UserCreatedHandler1.Reset();
        UserCreatedHandler2.Reset();

        var notifications = Enumerable.Range(1, 50)
            .Select(i => new UserCreatedNotification(i, $"User{i}"))
            .ToList();

        // Act
        var tasks = notifications.Select(n => _mediator.Publish(n));
        await Task.WhenAll(tasks);

        // Assert
        UserCreatedHandler1.HandleCount.Should().Be(50);
        UserCreatedHandler2.HandleCount.Should().Be(50);
    }

    [Fact]
    public async Task Publish_WithCancellationToken_PassesToHandlers()
    {
        // Arrange
        var notification = new UserCreatedNotification(1, "Test");
        using var cts = new CancellationTokenSource();
        UserCreatedHandler1.Reset();

        // Act
        await _mediator.Publish(notification, cts.Token);

        // Assert
        UserCreatedHandler1.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_WithNoHandlers_CompletesSuccessfully()
    {
        // Arrange - OrphanNotification has no registered handlers
        var notification = new OrphanNotification("Test");

        // Act
        Func<Task> act = async () => await _mediator.Publish(notification);

        // Assert - Should complete without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_WithInheritance_InvokesDerivedTypeHandler()
    {
        // Arrange - CreateOrderEvent inherits from DomainEventBase
        DomainEventBase domainEvent = new CreateOrderEvent(123, "John Doe");
        CreateOrderEventHandler.Reset();

        // Act - Publish using base class reference
        await _mediator.Publish(domainEvent);

        // Assert - Handler for CreateOrderEvent should be invoked, not DomainEventBase
        CreateOrderEventHandler.HandleCount.Should().Be(1);
        CreateOrderEventHandler.ReceivedEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CreateOrderEvent(123, "John Doe"));
    }

    [Fact]
    public async Task Publish_WithInheritance_ObjectOverload_InvokesDerivedTypeHandler()
    {
        // Arrange
        object domainEvent = new CreateOrderEvent(456, "Jane Doe");
        CreateOrderEventHandler.Reset();

        // Act
        await _mediator.Publish(domainEvent);

        // Assert
        CreateOrderEventHandler.HandleCount.Should().Be(1);
    }
}
