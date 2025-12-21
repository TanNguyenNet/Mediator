namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that executes all IRequestPostProcessor instances after the handler.
/// Uses array for performance and parallel execution for multiple processors.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class RequestPostProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRequestPostProcessor<TRequest, TResponse>[] _postProcessors;

    /// <summary>
    /// Creates a new instance of the post-processor behavior.
    /// </summary>
    /// <param name="postProcessors">The post-processors to execute.</param>
    public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors)
    {
        // Materialize to array once
        _postProcessors = postProcessors.ToArray();
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next().ConfigureAwait(false);

        // Fast path: no post-processors
        if (_postProcessors.Length == 0)
        {
            return response;
        }

        // Single post-processor: no need for parallel
        if (_postProcessors.Length == 1)
        {
            await _postProcessors[0].Process(request, response, cancellationToken).ConfigureAwait(false);
            return response;
        }

        // Multiple post-processors: run in parallel
        var tasks = new Task[_postProcessors.Length];
        for (int i = 0; i < _postProcessors.Length; i++)
        {
            tasks[i] = _postProcessors[i].Process(request, response, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return response;
    }
}
