namespace Mediator;

/// <summary>
/// Defines a mediator to encapsulate request/response and publish/subscribe patterns.
/// Combines both ISender and IPublisher interfaces.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
