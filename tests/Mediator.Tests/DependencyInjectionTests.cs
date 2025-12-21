using FluentAssertions;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for DI registration.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void AddMediator_RegistersIMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(typeof(DependencyInjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public void AddMediator_RegistersISender()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(typeof(DependencyInjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var sender = provider.GetService<ISender>();
        sender.Should().NotBeNull();
    }

    [Fact]
    public void AddMediator_RegistersIPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(typeof(DependencyInjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IPublisher>();
        publisher.Should().NotBeNull();
    }

    [Fact]
    public void AddMediator_RegistersRequestHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(typeof(DependencyInjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetService<IRequestHandler<PingRequest, PongResponse>>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<PingHandler>();
    }

    [Fact]
    public void AddMediator_RegistersNotificationHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(typeof(DependencyInjectionTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var handlers = provider.GetServices<INotificationHandler<UserCreatedNotification>>().ToList();
        handlers.Should().HaveCount(2);
    }

    [Fact]
    public void AddMediator_WithConfiguration_AppliesLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(DependencyInjectionTests).Assembly);
            config.MediatorLifetime = ServiceLifetime.Singleton;
        });

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMediator_RegistersBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(DependencyInjectionTests).Assembly);
            config.AddBehavior(typeof(TrackingBehavior<,>));
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var behaviors = provider.GetServices<IPipelineBehavior<PingRequest, PongResponse>>().ToList();
        behaviors.Should().Contain(b => b.GetType().GetGenericTypeDefinition() == typeof(TrackingBehavior<,>));
    }

    [Fact]
    public void AddMediator_MultipleAssemblies_RegistersFromAll()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(
            typeof(DependencyInjectionTests).Assembly,
            typeof(DependencyInjectionTests).Assembly // Same assembly twice for test
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public void AddMediator_ISenderAndIPublisher_AreSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(DependencyInjectionTests).Assembly);
            config.MediatorLifetime = ServiceLifetime.Singleton;
        });
        var provider = services.BuildServiceProvider();

        // Act
        var mediator = provider.GetService<IMediator>();
        var sender = provider.GetService<ISender>();
        var publisher = provider.GetService<IPublisher>();

        // Assert
        sender.Should().BeSameAs(mediator);
        publisher.Should().BeSameAs(mediator);
    }

    [Fact]
    public void AddMediator_WithFluentConfiguration_Works()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(DependencyInjectionTests).Assembly)
                .AddBehavior(typeof(TrackingBehavior<,>))
                .AddBehavior(typeof(FirstBehavior<,>));
            config.MediatorLifetime = ServiceLifetime.Scoped;
            config.HandlerLifetime = ServiceLifetime.Scoped;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public void MediatorServiceConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new MediatorServiceConfiguration();

        // Assert
        config.MediatorLifetime.Should().Be(ServiceLifetime.Transient);
        config.HandlerLifetime.Should().Be(ServiceLifetime.Transient);
        config.BehaviorLifetime.Should().Be(ServiceLifetime.Transient);
        config.RegisterGenericBehaviors.Should().BeTrue();
        config.MediatorImplementationType.Should().Be(typeof(Mediator));
    }
}
