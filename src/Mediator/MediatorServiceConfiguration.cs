using Microsoft.Extensions.DependencyInjection;

namespace Mediator;

/// <summary>
/// Configuration options for Mediator registration.
/// </summary>
public sealed class MediatorServiceConfiguration
{
    /// <summary>
    /// Gets or sets the lifetime for the Mediator service.
    /// Default is Transient for thread safety.
    /// </summary>
    public ServiceLifetime MediatorLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the lifetime for request handlers.
    /// Default is Transient.
    /// </summary>
    public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the lifetime for pipeline behaviors.
    /// Default is Transient.
    /// </summary>
    public ServiceLifetime BehaviorLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the type of Mediator implementation to use.
    /// Default is the built-in Mediator class.
    /// </summary>
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);

    /// <summary>
    /// Gets or sets whether to register open generic behaviors.
    /// Default is true.
    /// </summary>
    public bool RegisterGenericBehaviors { get; set; } = true;

    /// <summary>
    /// Gets the list of assemblies to scan for handlers.
    /// </summary>
    internal List<System.Reflection.Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Gets the list of behavior types to register.
    /// </summary>
    internal List<Type> BehaviorTypes { get; } = new();

    /// <summary>
    /// Adds an assembly to scan for handlers.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The configuration for chaining.</returns>
    public MediatorServiceConfiguration AddAssembly(System.Reflection.Assembly assembly)
    {
        AssembliesToScan.Add(assembly);
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior type.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type.</typeparam>
    /// <returns>The configuration for chaining.</returns>
    public MediatorServiceConfiguration AddBehavior<TBehavior>()
        where TBehavior : class
    {
        BehaviorTypes.Add(typeof(TBehavior));
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior type.
    /// </summary>
    /// <param name="behaviorType">The behavior type.</param>
    /// <returns>The configuration for chaining.</returns>
    public MediatorServiceConfiguration AddBehavior(Type behaviorType)
    {
        BehaviorTypes.Add(behaviorType);
        return this;
    }
}
