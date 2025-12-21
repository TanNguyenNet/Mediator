# Mediator

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/Tests-79%20Passed-brightgreen)](tests/Mediator.Tests)

High-performance **Mediator pattern** implementation for .NET with **CQRS support**. Inspired by [MediatR](https://github.com/jbogard/MediatR) with focus on performance and simplicity.

## ✨ Features

- 🚀 **High Performance** - Static caching, minimal allocations, aggressive inlining
- 📦 **CQRS Ready** - Request/Response, Commands, Queries, and Notifications
- 🔌 **Pipeline Behaviors** - Cross-cutting concerns (logging, validation, caching)
- ⚡ **Streaming Support** - `IAsyncEnumerable` for large datasets
- 🛡️ **Validation** - Built-in validation behavior with custom validators
- 🎯 **Pre/Post Processors** - Execute logic before/after handlers
- 💉 **DI Integration** - Native Microsoft.Extensions.DependencyInjection support

## 📦 Installation

```bash
dotnet add package Mediator
dotnet add package Mediator.Extensions.DependencyInjection
```

## 🚀 Quick Start

### 1. Define a Request and Handler

```csharp
// Request
public record GetUserQuery(int UserId) : IRequest<UserDto>;

// Handler
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        return new UserDto(request.UserId, "John Doe");
    }
}
```

### 2. Register Services

```csharp
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
});
```

### 3. Send Requests

```csharp
public class UserController
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator) => _mediator = mediator;

    public async Task<UserDto> GetUser(int id)
    {
        return await _mediator.Send(new GetUserQuery(id));
    }
}
```

## 📖 Usage Examples

### Commands (No Response)

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Create user logic
        return Unit.Value;
    }
}
```

### Notifications (Pub/Sub)

```csharp
public record UserCreated(int UserId, string Name) : INotification;

public class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken ct)
    {
        // Send email
    }
}

public class LogUserCreated : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken ct)
    {
        // Log event
    }
}

// Usage
await _mediator.Publish(new UserCreated(1, "John"));
```

### Streaming (IAsyncEnumerable)

```csharp
public record GetLogsStream(DateTime From) : IStreamRequest<LogEntry>;

public class GetLogsHandler : IStreamRequestHandler<GetLogsStream, LogEntry>
{
    public async IAsyncEnumerable<LogEntry> Handle(
        GetLogsStream request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var log in _db.GetLogsAsync(request.From, ct))
        {
            yield return log;
        }
    }
}
```

### Pipeline Behaviors

```csharp
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        
        var response = await next();
        
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Registration
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
    config.AddBehavior(typeof(LoggingBehavior<,>));
});
```

### Validation

```csharp
public class CreateUserValidator : IValidator<CreateUserCommand>
{
    public Task<ValidationResult> ValidateAsync(
        CreateUserCommand request, 
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Name))
            return Task.FromResult(
                ValidationResult.Failure("Name", "Name is required"));
        
        return Task.FromResult(ValidationResult.Success);
    }
}

// Registration
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
    config.AddBehavior(typeof(ValidationBehavior<,>));
});
services.AddTransient<IValidator<CreateUserCommand>, CreateUserValidator>();
```

## ⚙️ Configuration

```csharp
services.AddMediator(config =>
{
    // Scan assemblies for handlers
    config.AddAssembly(typeof(Program).Assembly);
    
    // Configure lifetimes
    config.MediatorLifetime = ServiceLifetime.Scoped;
    config.HandlerLifetime = ServiceLifetime.Transient;
    config.BehaviorLifetime = ServiceLifetime.Transient;
    
    // Add behaviors (executed in order)
    config.AddBehavior(typeof(LoggingBehavior<,>));
    config.AddBehavior(typeof(ValidationBehavior<,>));
    config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
    config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
});
```

## 🧪 Testing

The library includes **79 comprehensive unit tests** covering:

- ✅ Request/Response handling
- ✅ Notifications (multiple handlers)
- ✅ Pipeline behaviors
- ✅ Pre/Post processors
- ✅ Validation
- ✅ Streaming with IAsyncEnumerable
- ✅ Cancellation support
- ✅ Exception handling

```bash
cd tests/Mediator.Tests
dotnet test
```

## 📊 Project Structure

```
Mediator/
├── src/
│   ├── Mediator/
│   │   ├── Abstractions/          # Interfaces
│   │   ├── Behaviors/             # Pipeline behaviors
│   │   ├── Exceptions/            # Custom exceptions
│   │   ├── Validation/            # Validation support
│   │   ├── Wrappers/              # Handler wrappers
│   │   ├── Mediator.cs            # Main implementation
│   │   └── Unit.cs                # Unit type for void returns
│   └── Mediator.Extensions.DependencyInjection/
│       ├── MediatorServiceConfiguration.cs
│       └── ServiceCollectionExtensions.cs
└── tests/
    └── Mediator.Tests/            # Unit tests
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.
