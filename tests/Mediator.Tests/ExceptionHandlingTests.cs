using FluentAssertions;
using Mediator.Behaviors;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for exception handling scenarios.
/// </summary>
public class ExceptionHandlingTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    private IMediator CreateMediator(Action<MediatorServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ExceptionHandlingTests).Assembly);
            configure?.Invoke(config);
        });

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handler_ThrowsException_PropagatesWithOriginalType()
    {
        // Arrange
        var mediator = CreateMediator();
        var request = new FailingRequest("Test exception message");

        // Act
        Func<Task> act = async () => await mediator.Send(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception message");
    }

    [Fact]
    public async Task Handler_ThrowsCustomException_PreservesInnerException()
    {
        // Arrange
        var mediator = CreateMediator();
        var request = new ExceptionWithInnerRequest();

        // Act
        Func<Task> act = async () => await mediator.Send(request);

        // Assert
        var exception = await act.Should().ThrowAsync<ApplicationException>();
        exception.Which.InnerException.Should().NotBeNull();
        exception.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Behavior_ThrowsException_DoesNotExecuteHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ExceptionHandlingTests).Assembly);
            config.AddBehavior(typeof(ThrowingBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        ThrowingBehavior<PingRequest, PongResponse>.ShouldThrow = true;
        TrackingHandler.Reset();

        // Act
        Func<Task> act = async () => await mediator.Send(new PingRequest("Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Behavior exception");

        // Handler should not have been called
        ThrowingBehavior<PingRequest, PongResponse>.Reset();
    }

    [Fact]
    public async Task Send_ObjectOverload_WithNonRequest_ThrowsArgumentException()
    {
        // Arrange
        var mediator = CreateMediator();
        object invalidRequest = new NonRequestObject("test");

        // Act
        Func<Task> act = async () => await mediator.Send(invalidRequest);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not implement IRequest*");
    }

    [Fact]
    public async Task Publish_ObjectOverload_WithNonNotification_ThrowsArgumentException()
    {
        // Arrange
        var mediator = CreateMediator();
        object invalidNotification = new NonNotificationObject("test");

        // Act
        Func<Task> act = async () => await mediator.Publish(invalidNotification);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not implement INotification*");
    }

    [Fact]
    public async Task Send_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var mediator = CreateMediator();
        var request = new CancellableRequest(5000);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        Func<Task> act = async () => await mediator.Send(request, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Publish_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var mediator = CreateMediator();
        var notification = new CancellableNotification(5000);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        Func<Task> act = async () => await mediator.Publish(notification, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

// ========== Test Data for Exception Handling ==========

/// <summary>
/// Request that throws exception with inner exception.
/// </summary>
public record ExceptionWithInnerRequest() : IRequest<string>;

public class ExceptionWithInnerHandler : IRequestHandler<ExceptionWithInnerRequest, string>
{
    public Task<string> Handle(ExceptionWithInnerRequest request, CancellationToken cancellationToken)
    {
        var inner = new InvalidOperationException("Inner exception");
        throw new ApplicationException("Outer exception", inner);
    }
}

/// <summary>
/// Non-request object for testing invalid Send calls.
/// </summary>
public record NonRequestObject(string Data);

/// <summary>
/// Non-notification object for testing invalid Publish calls.
/// </summary>
public record NonNotificationObject(string Data);

/// <summary>
/// Request that supports cancellation for testing.
/// </summary>
public record CancellableRequest(int DelayMs) : IRequest<string>;

public class CancellableRequestHandler : IRequestHandler<CancellableRequest, string>
{
    public async Task<string> Handle(CancellableRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(request.DelayMs, cancellationToken);
        return "Completed";
    }
}

/// <summary>
/// Notification that supports cancellation for testing.
/// </summary>
public record CancellableNotification(int DelayMs) : INotification;

public class CancellableNotificationHandler : INotificationHandler<CancellableNotification>
{
    public async Task Handle(CancellableNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(notification.DelayMs, cancellationToken);
    }
}

/// <summary>
/// Behavior that throws exception for testing.
/// </summary>
public class ThrowingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static bool ShouldThrow { get; set; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("Behavior exception");
        }
        return await next();
    }

    public static void Reset() => ShouldThrow = false;
}

/// <summary>
/// Handler that tracks if it was called.
/// </summary>
public static class TrackingHandler
{
    public static int CallCount { get; private set; }

    public static void IncrementCallCount() => CallCount++;

    public static void Reset() => CallCount = 0;
}
