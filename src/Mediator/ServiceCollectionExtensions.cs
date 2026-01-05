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
        var config = new MediatorServiceConfiguration();
        configure(config);

        // Register IMediator, ISender, IPublisher
        RegisterMediator(services, config);

        // Scan assemblies and register handlers
        foreach (var assembly in config.AssembliesToScan)
        {
            RegisterHandlersFromAssembly(services, assembly, config);
        }

        // Register behaviors
        RegisterBehaviors(services, config);

        return services;
    }

    /// <summary>
    /// Registers the Mediator service.
    /// </summary>
    private static void RegisterMediator(IServiceCollection services, MediatorServiceConfiguration config)
    {
        var lifetime = config.MediatorLifetime;
        var implementationType = config.MediatorImplementationType;

        // Register IMediator
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), implementationType, lifetime));

        // Register ISender pointing to IMediator
        services.TryAdd(new ServiceDescriptor(
            typeof(ISender),
            sp => sp.GetRequiredService<IMediator>(),
            lifetime));

        // Register IPublisher pointing to IMediator
        services.TryAdd(new ServiceDescriptor(
            typeof(IPublisher),
            sp => sp.GetRequiredService<IMediator>(),
            lifetime));
    }

    /// <summary>
    /// Registers handlers from an assembly.
    /// </summary>
    private static void RegisterHandlersFromAssembly(
        IServiceCollection services,
        Assembly assembly,
        MediatorServiceConfiguration config)
    {
        var allTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false })
            .ToList();

        var handlerTypes = allTypes
            .Where(t => !t.IsGenericTypeDefinition)
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            // Register IRequestHandler<TRequest, TResponse>
            RegisterRequestHandlers(services, handlerType, config);

            // Register INotificationHandler<TNotification>
            RegisterNotificationHandlers(services, handlerType, config);

            // Register IStreamRequestHandler<TRequest, TResponse>
            RegisterStreamRequestHandlers(services, handlerType, config);
        }

        var registerPreProcessors = HasBehavior(config, typeof(RequestPreProcessorBehavior<,>));
        var registerPostProcessors = HasBehavior(config, typeof(RequestPostProcessorBehavior<,>));

        if (!registerPreProcessors && !registerPostProcessors)
        {
            return;
        }

        foreach (var processorType in allTypes)
        {
            if (registerPreProcessors)
            {
                RegisterPreProcessors(services, processorType, config);
            }

            if (registerPostProcessors)
            {
                RegisterPostProcessors(services, processorType, config);
            }
        }
    }

    /// <summary>
    /// Registers request handlers.
    /// </summary>
    private static void RegisterRequestHandlers(
        IServiceCollection services,
        Type handlerType,
        MediatorServiceConfiguration config)
    {
        var interfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToList();

        foreach (var @interface in interfaces)
        {
            services.TryAdd(new ServiceDescriptor(@interface, handlerType, config.HandlerLifetime));
        }
    }

    /// <summary>
    /// Registers notification handlers.
    /// </summary>
    private static void RegisterNotificationHandlers(
        IServiceCollection services,
        Type handlerType,
        MediatorServiceConfiguration config)
    {
        var interfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
            .ToList();

        foreach (var @interface in interfaces)
        {
            // Use Add instead of TryAdd for notifications (multiple handlers allowed)
            services.Add(new ServiceDescriptor(@interface, handlerType, config.HandlerLifetime));
        }
    }

    /// <summary>
    /// Registers stream request handlers.
    /// </summary>
    private static void RegisterStreamRequestHandlers(
        IServiceCollection services,
        Type handlerType,
        MediatorServiceConfiguration config)
    {
        var interfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
            .ToList();

        foreach (var @interface in interfaces)
        {
            services.TryAdd(new ServiceDescriptor(@interface, handlerType, config.HandlerLifetime));
        }
    }

    /// <summary>
    /// Registers pipeline behaviors.
    /// </summary>
    private static void RegisterBehaviors(
        IServiceCollection services,
        MediatorServiceConfiguration config)
    {
        foreach (var behaviorType in config.BehaviorTypes)
        {
            if (behaviorType.IsGenericTypeDefinition)
            {
                // Open generic behavior
                if (config.RegisterGenericBehaviors)
                {
                    services.Add(new ServiceDescriptor(
                        typeof(IPipelineBehavior<,>),
                        behaviorType,
                        config.BehaviorLifetime));
                }
            }
            else
            {
                // Closed generic behavior - find the IPipelineBehavior interface
                var pipelineInterface = behaviorType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                                         i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

                if (pipelineInterface is not null)
                {
                    services.Add(new ServiceDescriptor(
                        pipelineInterface,
                        behaviorType,
                        config.BehaviorLifetime));
                }
            }
        }
    }

    /// <summary>
    /// Registers request pre-processors.
    /// </summary>
    private static void RegisterPreProcessors(
        IServiceCollection services,
        Type handlerType,
        MediatorServiceConfiguration config)
    {
        var interfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IRequestPreProcessor<>))
            .ToList();

        foreach (var @interface in interfaces)
        {
            var serviceType = handlerType.IsGenericTypeDefinition
                ? @interface.GetGenericTypeDefinition()
                : @interface;

            // Multiple pre-processors allowed; avoid duplicate registrations
            services.TryAddEnumerable(new ServiceDescriptor(serviceType, handlerType, config.HandlerLifetime));
        }
    }

    /// <summary>
    /// Registers request post-processors.
    /// </summary>
    private static void RegisterPostProcessors(
        IServiceCollection services,
        Type handlerType,
        MediatorServiceConfiguration config)
    {
        var interfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IRequestPostProcessor<,>))
            .ToList();

        foreach (var @interface in interfaces)
        {
            var serviceType = handlerType.IsGenericTypeDefinition
                ? @interface.GetGenericTypeDefinition()
                : @interface;

            // Multiple post-processors allowed; avoid duplicate registrations
            services.TryAddEnumerable(new ServiceDescriptor(serviceType, handlerType, config.HandlerLifetime));
        }
    }

    private static bool HasBehavior(MediatorServiceConfiguration config, Type openGenericBehaviorType)
    {
        foreach (var behaviorType in config.BehaviorTypes)
        {
            if (behaviorType == openGenericBehaviorType)
            {
                return true;
            }

            if (behaviorType.IsGenericType &&
                behaviorType.GetGenericTypeDefinition() == openGenericBehaviorType)
            {
                return true;
            }
        }

        return false;
    }
}
