namespace Mediator.Tests.TestData;

/// <summary>
/// Pipeline behavior that tracks execution order.
/// </summary>
public class TrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static List<string> ExecutionLog { get; } = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ExecutionLog.Add($"Before:{typeof(TRequest).Name}");
        var response = await next();
        ExecutionLog.Add($"After:{typeof(TRequest).Name}");
        return response;
    }

    public static void Reset() => ExecutionLog.Clear();
}

/// <summary>
/// First behavior for testing order.
/// </summary>
public class FirstBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static List<string> ExecutionLog { get; } = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ExecutionLog.Add("First:Before");
        var response = await next();
        ExecutionLog.Add("First:After");
        return response;
    }

    public static void Reset() => ExecutionLog.Clear();
}

/// <summary>
/// Second behavior for testing order.
/// </summary>
public class SecondBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static List<string> ExecutionLog { get; } = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ExecutionLog.Add("Second:Before");
        var response = await next();
        ExecutionLog.Add("Second:After");
        return response;
    }

    public static void Reset() => ExecutionLog.Clear();
}

/// <summary>
/// Behavior that short-circuits the pipeline.
/// </summary>
public class ShortCircuitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static bool ShouldShortCircuit { get; set; }
    public static TResponse? ShortCircuitResponse { get; set; }

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (ShouldShortCircuit && ShortCircuitResponse != null)
        {
            return Task.FromResult(ShortCircuitResponse);
        }

        return next();
    }

    public static void Reset()
    {
        ShouldShortCircuit = false;
        ShortCircuitResponse = default;
    }
}
