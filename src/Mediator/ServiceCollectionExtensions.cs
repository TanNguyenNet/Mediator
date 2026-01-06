using System.Reflection;
using Mediator.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mediator;

/// <summary>
/// Extension methods for registering Mediator in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly Type[] HandlerInterfaceTypes =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>),
        typeof(IStreamRequestHandler<,>)
    ];

    private static readonly Type[] MultipleRegistrationTypes =
    [
        typeof(INotificationHandler<>)
    ];

    /// <summary>
    /// Adds Mediator services to the specified IServiceCollection.
    /// Scans the provided assemblies for handlers.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="assemblies">The assemblies to scan for handlers.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return services.AddMediator(config =>
        {
            foreach (var assembly in assemblies)
            {
                config.AddAssembly(assembly);
            }
        });
    }

    /// <summary>
    /// Adds Mediator services to the specified IServiceCollection with configuration.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorServiceConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new MediatorServiceConfiguration();
        configure(config);

        return services
            .RegisterMediator(config)
            .RegisterAllHandlers(config)
            .RegisterAllProcessors(config)
            .RegisterBehaviors(config);
    }

    private static IServiceCollection RegisterMediator(
        this IServiceCollection services,
        MediatorServiceConfiguration config)
    {
        var lifetime = config.MediatorLifetime;
        var implementationType = config.MediatorImplementationType;

        services.TryAdd(new ServiceDescriptor(typeof(IMediator), implementationType, lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), lifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), lifetime));

        return services;
    }

    private static IServiceCollection RegisterAllHandlers(
        this IServiceCollection services,
        MediatorServiceConfiguration config)
    {
        var concreteTypes = config.AssembliesToScan
            .SelectMany(GetConcreteTypes)
            .Where(t => !t.IsGenericTypeDefinition)
            .ToList();

        foreach (var handlerType in concreteTypes)
        {
            services.RegisterHandlerInterfaces(handlerType, config.HandlerLifetime);
        }

        return services;
    }

    private static IServiceCollection RegisterAllProcessors(
        this IServiceCollection services,
        MediatorServiceConfiguration config)
    {
        var processorRegistrations = new (Type BehaviorType, Type ProcessorInterface)[]
        {
            (typeof(RequestPreProcessorBehavior<,>), typeof(IRequestPreProcessor<>)),
            (typeof(RequestPostProcessorBehavior<,>), typeof(IRequestPostProcessor<,>))
        };

        var concreteTypes = config.AssembliesToScan
            .SelectMany(GetConcreteTypes)
            .ToList();

        foreach (var (behaviorType, processorInterface) in processorRegistrations)
        {
            if (!config.HasBehavior(behaviorType))
            {
                continue;
            }

            foreach (var processorType in concreteTypes)
            {
                services.RegisterProcessorInterfaces(processorType, processorInterface, config.HandlerLifetime);
            }
        }

        return services;
    }

    private static IServiceCollection RegisterBehaviors(
        this IServiceCollection services,
        MediatorServiceConfiguration config)
    {
        foreach (var behaviorType in config.BehaviorTypes)
        {
            var (serviceType, implType) = ResolveBehaviorTypes(behaviorType, config);

            if (serviceType is not null)
            {
                services.Add(new ServiceDescriptor(serviceType, implType, config.BehaviorLifetime));
            }
        }

        return services;
    }

    private static (Type? ServiceType, Type ImplementationType) ResolveBehaviorTypes(
        Type behaviorType,
        MediatorServiceConfiguration config)
    {
        if (behaviorType.IsGenericTypeDefinition)
        {
            return config.RegisterGenericBehaviors
                ? (typeof(IPipelineBehavior<,>), behaviorType)
                : (null, behaviorType);
        }

        var pipelineInterface = behaviorType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        return (pipelineInterface, behaviorType);
    }

    private static void RegisterHandlerInterfaces(
        this IServiceCollection services,
        Type handlerType,
        ServiceLifetime lifetime)
    {
        foreach (var interfaceType in HandlerInterfaceTypes)
        {
            var allowMultiple = MultipleRegistrationTypes.Contains(interfaceType);
            services.RegisterInterfaces(handlerType, interfaceType, lifetime, allowMultiple);
        }
    }

    private static void RegisterInterfaces(
        this IServiceCollection services,
        Type implementationType,
        Type openGenericInterface,
        ServiceLifetime lifetime,
        bool allowMultiple)
    {
        foreach (var serviceType in implementationType.GetGenericInterfaces(openGenericInterface))
        {
            var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);

            if (allowMultiple)
            {
                services.Add(descriptor);
            }
            else
            {
                services.TryAdd(descriptor);
            }
        }
    }

    private static void RegisterProcessorInterfaces(
        this IServiceCollection services,
        Type processorType,
        Type openGenericInterface,
        ServiceLifetime lifetime)
    {
        foreach (var serviceType in processorType.GetGenericInterfaces(openGenericInterface))
        {
            var registrationType = processorType.IsGenericTypeDefinition
                ? serviceType.GetGenericTypeDefinition()
                : serviceType;

            services.TryAddEnumerable(new ServiceDescriptor(registrationType, processorType, lifetime));
        }
    }

    private static IEnumerable<Type> GetConcreteTypes(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false });
    }

    private static IEnumerable<Type> GetGenericInterfaces(this Type implementationType, Type openGenericInterface)
    {
        return implementationType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);
    }

    private static bool HasBehavior(this MediatorServiceConfiguration config, Type openGenericBehaviorType)
    {
        return config.BehaviorTypes.Any(bt =>
            bt == openGenericBehaviorType ||
            (bt.IsGenericType && bt.GetGenericTypeDefinition() == openGenericBehaviorType));
    }
}
