namespace Mediator.Tests.TestData;

// ========== PRE-PROCESSORS ==========

/// <summary>
/// Pre-processor that tracks execution.
/// </summary>
public class TrackingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public static int ExecutionCount { get; private set; }
    public static List<Type> ProcessedTypes { get; } = new();

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        ProcessedTypes.Add(typeof(TRequest));
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ExecutionCount = 0;
        ProcessedTypes.Clear();
    }
}

/// <summary>
/// Pre-processor specific to PingRequest.
/// </summary>
public class PingPreProcessor : IRequestPreProcessor<PingRequest>
{
    public static int ExecutionCount { get; private set; }
    public static string? LastMessage { get; private set; }

    public Task Process(PingRequest request, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        LastMessage = request.Message;
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ExecutionCount = 0;
        LastMessage = null;
    }
}

// ========== POST-PROCESSORS ==========

/// <summary>
/// Post-processor that tracks execution.
/// </summary>
public class TrackingPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public static int ExecutionCount { get; private set; }
    public static List<(Type RequestType, Type ResponseType)> ProcessedTypes { get; } = new();

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        ProcessedTypes.Add((typeof(TRequest), typeof(TResponse)));
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ExecutionCount = 0;
        ProcessedTypes.Clear();
    }
}

/// <summary>
/// Post-processor specific to PingRequest/PongResponse.
/// </summary>
public class PingPostProcessor : IRequestPostProcessor<PingRequest, PongResponse>
{
    public static int ExecutionCount { get; private set; }
    public static PongResponse? LastResponse { get; private set; }

    public Task Process(PingRequest request, PongResponse response, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        LastResponse = response;
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ExecutionCount = 0;
        LastResponse = null;
    }
}
