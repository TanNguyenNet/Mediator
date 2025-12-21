using Mediator.Validation;

namespace Mediator.Tests.TestData;

/// <summary>
/// Validator for ValidatedRequest.
/// </summary>
public class ValidatedRequestValidator : IValidator<ValidatedRequest>
{
    public Task<ValidationResult> ValidateAsync(ValidatedRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new ValidationFailure(nameof(request.Name), "Name is required"));
        }

        if (request.Age < 0)
        {
            errors.Add(new ValidationFailure(nameof(request.Age), "Age must be non-negative", request.Age));
        }

        if (request.Age > 150)
        {
            errors.Add(new ValidationFailure(nameof(request.Age), "Age must be less than 150", request.Age));
        }

        return Task.FromResult(errors.Count > 0
            ? new ValidationResult(errors)
            : ValidationResult.Success);
    }
}

/// <summary>
/// Always-failing validator for testing.
/// </summary>
public class AlwaysFailingValidator<TRequest> : IValidator<TRequest>
    where TRequest : notnull
{
    public Task<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidationResult.Failure("Test", "Always fails"));
    }
}

/// <summary>
/// Always-passing validator for testing.
/// </summary>
public class AlwaysPassingValidator<TRequest> : IValidator<TRequest>
    where TRequest : notnull
{
    public Task<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ValidationResult.Success);
    }
}
