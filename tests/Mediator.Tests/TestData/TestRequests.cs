namespace Mediator.Tests.TestData;

// ========== REQUESTS ==========

/// <summary>
/// Simple request with response.
/// </summary>
public record PingRequest(string Message) : IRequest<PongResponse>;

/// <summary>
/// Response for PingRequest.
/// </summary>
public record PongResponse(string Reply);

/// <summary>
/// Request without response (returns Unit).
/// </summary>
public record VoidRequest(string Data) : IRequest;

/// <summary>
/// Request that throws exception.
/// </summary>
public record FailingRequest(string ErrorMessage) : IRequest<string>;

/// <summary>
/// Request for testing validation.
/// </summary>
public record ValidatedRequest(string Name, int Age) : IRequest<string>;

// ========== HANDLERS ==========

/// <summary>
/// Handler for PingRequest.
/// </summary>
public class PingHandler : IRequestHandler<PingRequest, PongResponse>
{
    public Task<PongResponse> Handle(PingRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PongResponse($"Pong: {request.Message}"));
    }
}

/// <summary>
/// Handler for VoidRequest.
/// </summary>
public class VoidRequestHandler : IRequestHandler<VoidRequest, Unit>
{
    public static int HandleCount { get; private set; }

    public Task<Unit> Handle(VoidRequest request, CancellationToken cancellationToken)
    {
        HandleCount++;
        return Unit.Task;
    }

    public static void Reset() => HandleCount = 0;
}

/// <summary>
/// Handler that throws exception.
/// </summary>
public class FailingHandler : IRequestHandler<FailingRequest, string>
{
    public Task<string> Handle(FailingRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(request.ErrorMessage);
    }
}

/// <summary>
/// Handler for validated request.
/// </summary>
public class ValidatedRequestHandler : IRequestHandler<ValidatedRequest, string>
{
    public Task<string> Handle(ValidatedRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Hello, {request.Name}! You are {request.Age} years old.");
    }
}
