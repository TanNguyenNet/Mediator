namespace Mediator;

/// <summary>
/// Marker interface for a request with a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest
{
}

/// <summary>
/// Marker interface for a request without a response (void/Unit).
/// </summary>
public interface IRequest : IRequest<Unit>
{
}
