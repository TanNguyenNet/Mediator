# Mediator

[![NuGet](https://img.shields.io/nuget/v/Mediator.Slim)](https://www.nuget.org/packages/Mediator.Slim)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/Tests-84%20Passed-brightgreen)](tests/Mediator.Tests)

High-performance **Mediator pattern** implementation for .NET with **CQRS support**. Inspired by [MediatR](https://github.com/jbogard/MediatR) with focus on performance and simplicity.

## ✨ Features

- 🚀 **High Performance** - Static caching, minimal allocations, aggressive inlining, zero-allocation publish for synchronous handlers
- 📦 **CQRS Ready** - Request/Response, Commands, Queries, and Notifications
- 🔌 **Pipeline Behaviors** - Cross-cutting concerns (logging, validation, caching)
- ⚡ **Streaming Support** - `IAsyncEnumerable` for large datasets
- 🛡️ **Validation** - Built-in validation behavior with custom validators
- 🎯 **Pre/Post Processors** - Execute logic before/after handlers
- 💉 **DI Integration** - Native Microsoft.Extensions.DependencyInjection support

## 📦 Installation

```bash
dotnet add package Mediator.Slim
```

DI registration (`AddMediator`) is included in the package — no extra package needed.

**Requirements:** .NET 10.0 or later. Use version `1.0.1` for .NET 8.0.

## 🆕 What's New in 2.0.0

- ⬆️ **.NET 10** - Targets `net10.0` (breaking: .NET 8 is no longer supported) with `Microsoft.Extensions.*` 10.0 packages.
- 🧬 **Polymorphic publish** - `Publish` dispatches on the notification's runtime type, so publishing through a base type (e.g. `DomainEventBase`) or as `object` reaches the concrete type's handlers.
- ⚡ **Faster notifications** - Publishing to 3+ handlers is non-async, uses `ArrayPool`, and allocates nothing when every handler completes synchronously.

| Handlers (sync) | 1.0.1 | 2.0.0 |
|---|---|---|
| 3 | 58 ns / 144 B | 16 ns / 0 B |
| 10 | 160 ns / 144 B | 37 ns / 0 B |
| 50 | 697 ns / 144 B | 182 ns / 0 B |

*BenchmarkDotNet on .NET 10, publish fan-out to N handlers; the 1.0.1 column is the 1.0.1 code path run on .NET 10. For async handlers time is unchanged and allocations are lower.*

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

All handlers are started and awaited together; `Publish` completes when every handler has completed and throws if any handler fails.

Publishing through a base type dispatches to the handlers of the runtime type:

```csharp
public abstract record DomainEventBase : INotification;
public record OrderCreated(int OrderId) : DomainEventBase;

DomainEventBase domainEvent = new OrderCreated(42);
await _mediator.Publish(domainEvent); // invokes INotificationHandler<OrderCreated>
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

The library includes **84 comprehensive unit tests** (xUnit v3) covering:

- ✅ Request/Response handling
- ✅ Notifications (single, multiple and 3+ handlers, polymorphic publish)
- ✅ Pipeline behaviors
- ✅ Pre/Post processors
- ✅ Validation
- ✅ Streaming with IAsyncEnumerable
- ✅ Cancellation support
- ✅ Exception handling

```bash
dotnet test
```

`global.json` opts `dotnet test` into Microsoft.Testing.Platform, which xUnit v3 requires on the .NET 10 SDK.

## 📊 Project Structure

```
Mediator/
├── src/
│   └── Mediator/                          # NuGet package: Mediator.Slim
│       ├── Abstractions/                  # Interfaces
│       ├── Behaviors/                     # Pipeline behaviors
│       ├── Exceptions/                    # Custom exceptions
│       ├── Validation/                    # Validation support
│       ├── Wrappers/                      # Handler wrappers
│       ├── Mediator.cs                    # Main implementation
│       ├── MediatorServiceConfiguration.cs
│       ├── ServiceCollectionExtensions.cs # AddMediator registration
│       └── Unit.cs                        # Unit type for void returns
├── tests/
│   └── Mediator.Tests/                    # Unit tests
└── benchmarks/
    └── Mediator.Benchmarks/               # BenchmarkDotNet vs MediatR
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.
