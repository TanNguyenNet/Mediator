using Mediator.Exceptions;
using Mediator.Validation;

namespace Mediator.Behaviors;

/// <summary>
/// Pipeline behavior that validates requests before handling.
/// Uses array instead of IEnumerable for better performance.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cached type name
    private static readonly string RequestTypeName = typeof(TRequest).Name;

    // Validators materialized as array for performance
    private readonly IValidator<TRequest>[] _validators;

    /// <summary>
    /// Creates a new instance of the validation behavior.
    /// </summary>
    /// <param name="validators">The validators for this request type.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        // Materialize to array once to avoid repeated enumeration
        _validators = validators.ToArray();
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Fast path: no validators registered
        if (_validators.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        // Single validator: fast path
        if (_validators.Length == 1)
        {
            var result = await _validators[0].ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors.Select(e => 
                    new Exceptions.ValidationError(e.PropertyName, e.ErrorMessage)));
            }
            return await next().ConfigureAwait(false);
        }

        // Multiple validators: run in parallel
        var validationTasks = new Task<ValidationResult>[_validators.Length];
        for (int i = 0; i < _validators.Length; i++)
        {
            validationTasks[i] = _validators[i].ValidateAsync(request, cancellationToken);
        }

        // Wait for all validations to complete
        await Task.WhenAll(validationTasks).ConfigureAwait(false);

        // Collect all failures
        var failures = new List<Exceptions.ValidationError>();
        for (int i = 0; i < validationTasks.Length; i++)
        {
            var result = validationTasks[i].Result;
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors.Select(e => 
                    new Exceptions.ValidationError(e.PropertyName, e.ErrorMessage)));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
