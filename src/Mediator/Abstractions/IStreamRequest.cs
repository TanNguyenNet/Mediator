namespace Mediator;

/// <summary>
/// Marker interface for stream requests that return IAsyncEnumerable.
/// </summary>
/// <typeparam name="TResponse">The type of items yielded by the stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
