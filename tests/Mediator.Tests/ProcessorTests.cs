using FluentAssertions;
using Mediator.Behaviors;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for Pre and Post Processors.
/// </summary>
public class ProcessorTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        TrackingPreProcessor<PingRequest>.Reset();
        TrackingPostProcessor<PingRequest, PongResponse>.Reset();
        PingPreProcessor.Reset();
        PingPostProcessor.Reset();
    }

    private IMediator CreateMediatorWithProcessors()
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
            config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task PreProcessor_ExecutesBeforeHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
        });
        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        PingPreProcessor.Reset();

        // Act
        await mediator.Send(new PingRequest("PreProcess Test"));

        // Assert
        PingPreProcessor.ExecutionCount.Should().Be(1);
        PingPreProcessor.LastMessage.Should().Be("PreProcess Test");
    }

    [Fact]
    public async Task PostProcessor_ExecutesAfterHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
        });
        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        PingPostProcessor.Reset();

        // Act
        await mediator.Send(new PingRequest("PostProcess Test"));

        // Assert
        PingPostProcessor.ExecutionCount.Should().Be(1);
        PingPostProcessor.LastResponse.Should().NotBeNull();
        PingPostProcessor.LastResponse!.Reply.Should().Be("Pong: PostProcess Test");
    }

    [Fact]
    public async Task PreAndPostProcessors_ExecuteInCorrectOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
            config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
        });

        // Custom processors that track order (not auto-registered because they are nested types)
        services.AddTransient<IRequestPreProcessor<PingRequest>>(sp =>
            new OrderTrackingPreProcessor(executionOrder));
        services.AddTransient<IRequestPostProcessor<PingRequest, PongResponse>>(sp =>
            new OrderTrackingPostProcessor(executionOrder));

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new PingRequest("Order Test"));

        // Assert
        executionOrder.Should().HaveCount(2);
        executionOrder[0].Should().Be("PreProcessor");
        executionOrder[1].Should().Be("PostProcessor");
    }

    [Fact]
    public async Task MultiplePreProcessors_AllExecute()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        PingPreProcessor.Reset();
        AnotherPingPreProcessor.Reset();

        // Act
        await mediator.Send(new PingRequest("Multiple Test"));

        // Assert
        PingPreProcessor.ExecutionCount.Should().Be(1);
        AnotherPingPreProcessor.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task NoProcessors_HandlerStillExecutes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ProcessorTests).Assembly);
            config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
            config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
        });

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new PingRequest("No processors"));

        // Assert
        response.Reply.Should().Be("Pong: No processors");
    }

    // Helper classes for order tracking
    private class OrderTrackingPreProcessor : IRequestPreProcessor<PingRequest>
    {
        private readonly List<string> _order;

        public OrderTrackingPreProcessor(List<string> order) => _order = order;

        public Task Process(PingRequest request, CancellationToken cancellationToken)
        {
            _order.Add("PreProcessor");
            return Task.CompletedTask;
        }
    }

    private class OrderTrackingPostProcessor : IRequestPostProcessor<PingRequest, PongResponse>
    {
        private readonly List<string> _order;

        public OrderTrackingPostProcessor(List<string> order) => _order = order;

        public Task Process(PingRequest request, PongResponse response, CancellationToken cancellationToken)
        {
            _order.Add("PostProcessor");
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Another pre-processor for testing multiple processors.
/// </summary>
public class AnotherPingPreProcessor : IRequestPreProcessor<PingRequest>
{
    public static int ExecutionCount { get; private set; }

    public Task Process(PingRequest request, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        return Task.CompletedTask;
    }

    public static void Reset() => ExecutionCount = 0;
}
