namespace Mediator.Validation;

/// <summary>
/// Interface for request validators.
/// Implement this interface to add validation logic for requests.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public interface IValidator<in TRequest>
    where TRequest : notnull
{
    /// <summary>
    /// Validates the request asynchronously.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result containing any errors.</returns>
    Task<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Errors { get; }

    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// A successful validation result with no errors.
    /// </summary>
    public static readonly ValidationResult Success = new(Array.Empty<ValidationFailure>());

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationResult(IEnumerable<ValidationFailure> errors)
    {
        Errors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A validation result with the error.</returns>
    public static ValidationResult Failure(string propertyName, string errorMessage)
    {
        return new ValidationResult(new[] { new ValidationFailure(propertyName, errorMessage) });
    }

    /// <summary>
    /// Creates a failed validation result with multiple errors.
    /// </summary>
    /// <param name="errors">The errors.</param>
    /// <returns>A validation result with the errors.</returns>
    public static ValidationResult Failure(params ValidationFailure[] errors)
    {
        return new ValidationResult(errors);
    }
}

/// <summary>
/// Represents a single validation failure.
/// </summary>
/// <param name="PropertyName">The name of the property that failed validation.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="AttemptedValue">The value that was attempted.</param>
public readonly record struct ValidationFailure(
    string PropertyName,
    string ErrorMessage,
    object? AttemptedValue = null);
