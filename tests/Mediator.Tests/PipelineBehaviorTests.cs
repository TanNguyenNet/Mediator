using FluentAssertions;
using Mediator.Behaviors;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for Pipeline Behaviors.
/// </summary>
public class PipelineBehaviorTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        TrackingBehavior<PingRequest, PongResponse>.Reset();
        FirstBehavior<PingRequest, PongResponse>.Reset();
        SecondBehavior<PingRequest, PongResponse>.Reset();
        ShortCircuitBehavior<PingRequest, PongResponse>.Reset();
    }

    private IMediator CreateMediator(Action<MediatorServiceConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(PipelineBehaviorTests).Assembly);
            configure?.Invoke(config);
        });

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Behavior_ExecutesBeforeAndAfterHandler()
    {
        // Arrange
        var mediator = CreateMediator(config =>
        {
            config.AddBehavior(typeof(TrackingBehavior<,>));
        });
        TrackingBehavior<PingRequest, PongResponse>.Reset();

        // Act
        await mediator.Send(new PingRequest("Test"));

        // Assert
        var log = TrackingBehavior<PingRequest, PongResponse>.ExecutionLog;
        log.Should().HaveCount(2);
        log[0].Should().StartWith("Before:");
        log[1].Should().StartWith("After:");
    }

    [Fact]
    public async Task MultipleBehaviors_ExecuteInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(PipelineBehaviorTests).Assembly);
            config.AddBehavior(typeof(FirstBehavior<,>));
            config.AddBehavior(typeof(SecondBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        FirstBehavior<PingRequest, PongResponse>.Reset();
        SecondBehavior<PingRequest, PongResponse>.Reset();

        // Act
        await mediator.Send(new PingRequest("Test"));

        // Assert
        var firstLog = FirstBehavior<PingRequest, PongResponse>.ExecutionLog;
        var secondLog = SecondBehavior<PingRequest, PongResponse>.ExecutionLog;

        firstLog.Should().Contain("First:Before");
        firstLog.Should().Contain("First:After");
        secondLog.Should().Contain("Second:Before");
        secondLog.Should().Contain("Second:After");
    }

    [Fact]
    public async Task Behavior_CanShortCircuitPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(PipelineBehaviorTests).Assembly);
            config.AddBehavior(typeof(ShortCircuitBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        ShortCircuitBehavior<PingRequest, PongResponse>.ShouldShortCircuit = true;
        ShortCircuitBehavior<PingRequest, PongResponse>.ShortCircuitResponse = new PongResponse("Short-circuited");

        // Act
        var response = await mediator.Send(new PingRequest("Original"));

        // Assert
        response.Reply.Should().Be("Short-circuited");

        // Cleanup
        ShortCircuitBehavior<PingRequest, PongResponse>.Reset();
    }

    [Fact]
    public async Task Behavior_WithNoBehaviors_HandlerStillExecutes()
    {
        // Arrange
        var mediator = CreateMediator();

        // Act
        var response = await mediator.Send(new PingRequest("No behaviors"));

        // Assert
        response.Reply.Should().Be("Pong: No behaviors");
    }

    [Fact]
    public async Task Behavior_PassesRequestToHandler()
    {
        // Arrange
        var mediator = CreateMediator(config =>
        {
            config.AddBehavior(typeof(TrackingBehavior<,>));
        });

        // Act
        var response = await mediator.Send(new PingRequest("Passed through"));

        // Assert
        response.Reply.Should().Be("Pong: Passed through");
    }
}
