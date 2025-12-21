namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that executes all IRequestPreProcessor instances before the handler.
/// Uses array for performance and parallel execution for multiple processors.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class RequestPreProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRequestPreProcessor<TRequest>[] _preProcessors;

    /// <summary>
    /// Creates a new instance of the pre-processor behavior.
    /// </summary>
    /// <param name="preProcessors">The pre-processors to execute.</param>
    public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors)
    {
        // Materialize to array once
        _preProcessors = preProcessors.ToArray();
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Fast path: no pre-processors
        if (_preProcessors.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        // Single pre-processor: no need for parallel
        if (_preProcessors.Length == 1)
        {
            await _preProcessors[0].Process(request, cancellationToken).ConfigureAwait(false);
            return await next().ConfigureAwait(false);
        }

        // Multiple pre-processors: run in parallel
        var tasks = new Task[_preProcessors.Length];
        for (int i = 0; i < _preProcessors.Length; i++)
        {
            tasks[i] = _preProcessors[i].Process(request, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return await next().ConfigureAwait(false);
    }
}
