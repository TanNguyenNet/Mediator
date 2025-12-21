using FluentAssertions;
using Mediator.Behaviors;
using Mediator.Exceptions;
using Mediator.Tests.TestData;
using Mediator.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for Validation Behavior.
/// </summary>
public class ValidationTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task ValidationBehavior_WithValidRequest_PassesThrough()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IValidator<ValidatedRequest>, ValidatedRequestValidator>();

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new ValidatedRequest("John", 25));

        // Assert
        result.Should().Be("Hello, John! You are 25 years old.");
    }

    [Fact]
    public async Task ValidationBehavior_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IValidator<ValidatedRequest>, ValidatedRequestValidator>();

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        Func<Task> act = async () => await mediator.Send(new ValidatedRequest("", 25));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("Name");
    }

    [Fact]
    public async Task ValidationBehavior_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IValidator<ValidatedRequest>, ValidatedRequestValidator>();

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        Func<Task> act = async () => await mediator.Send(new ValidatedRequest("", -5));

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ValidationBehavior_WithNoValidators_PassesThrough()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        // Not registering any validators

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new PingRequest("No validation"));

        // Assert
        result.Reply.Should().Be("Pong: No validation");
    }

    [Fact]
    public async Task ValidationBehavior_WithMultipleValidators_RunsAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IValidator<ValidatedRequest>, ValidatedRequestValidator>();
        services.AddTransient<IValidator<ValidatedRequest>, AlwaysPassingValidator<ValidatedRequest>>();

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act - valid request passes both validators
        var result = await mediator.Send(new ValidatedRequest("John", 25));

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidationBehavior_WithAlwaysFailingValidator_AlwaysFails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(ValidationTests).Assembly);
            config.AddBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IValidator<PingRequest>, AlwaysFailingValidator<PingRequest>>();

        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Act
        Func<Task> act = async () => await mediator.Send(new PingRequest("Will fail"));

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void ValidationResult_Success_IsValid()
    {
        // Act
        var result = ValidationResult.Success;

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_Failure_IsNotValid()
    {
        // Act
        var result = ValidationResult.Failure("Name", "Name is required");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void ValidationException_ContainsErrors()
    {
        // Arrange
        var errors = new[]
        {
            new Exceptions.ValidationError("Field1", "Error1"),
            new Exceptions.ValidationError("Field2", "Error2")
        };

        // Act
        var exception = new ValidationException(errors);

        // Assert
        exception.Errors.Should().HaveCount(2);
        exception.Message.Should().Contain("validation");
    }
}
