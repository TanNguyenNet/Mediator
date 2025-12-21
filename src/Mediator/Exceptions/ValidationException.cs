namespace Mediator.Exceptions;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Creates a new validation exception with a single error.
    /// </summary>
    /// <param name="propertyName">The property name that failed validation.</param>
    /// <param name="errorMessage">The error message.</param>
    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for {propertyName}: {errorMessage}")
    {
        Errors = new[] { new ValidationError(propertyName, errorMessage) };
    }

    /// <summary>
    /// Creates a new validation exception with multiple errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(IEnumerable<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a new validation exception with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ValidationException(string message)
        : base(message)
    {
        Errors = Array.Empty<ValidationError>();
    }
}

/// <summary>
/// Represents a single validation error.
/// </summary>
/// <param name="PropertyName">The name of the property that failed validation.</param>
/// <param name="ErrorMessage">The error message.</param>
public readonly record struct ValidationError(string PropertyName, string ErrorMessage);
