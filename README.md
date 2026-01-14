# Mediator

[![NuGet](https://img.shields.io/nuget/v/Mediator.svg)](https://www.nuget.org/packages/Mediator/)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Tests](https://img.shields.io/badge/Tests-79%20Passed-brightgreen)](tests/Mediator.Tests)

A **high-performance Mediator pattern** implementation for .NET with **CQRS support**. Inspired by [MediatR](https://github.com/jbogard/MediatR) with a focus on **performance**, **simplicity**, and **zero-allocation hot paths**.

## Why Mediator?

| Feature | Mediator | MediatR |
|---------|----------|---------|
| Static handler caching | Yes | No |
| Typed handler path (no boxing) | Yes | Limited |
| ArrayPool for notifications | Yes | No |
| ObjectPool for Stopwatch | Yes | No |
| Built-in behaviors | 6 | 0 |
| IAsyncEnumerable streaming | Yes | Yes |
| .NET 8 optimized | Yes | Yes |

## Features

- **High Performance** - Static caching, minimal allocations, aggressive inlining
- **CQRS Ready** - Request/Response, Commands, Queries, and Notifications
- **Pipeline Behaviors** - Cross-cutting concerns (logging, validation, performance monitoring)
- **Streaming Support** - Native `IAsyncEnumerable` for large datasets
- **Validation** - Built-in validation framework with custom validators
- **Pre/Post Processors** - Execute logic before/after handlers
- **DI Integration** - Native Microsoft.Extensions.DependencyInjection support

## Installation

```bash
dotnet add package Mediator
```

> **Note**: DI support is built-in. No separate package required.

## Quick Start

### 1. Define a Request and Handler

```csharp
using Mediator;

// Query with response
public record GetUserQuery(int UserId) : IRequest<UserDto>;

// Handler
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _repository;

    public GetUserHandler(IUserRepository repository) => _repository = repository;

    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(request.UserId, ct);
        return new UserDto(user.Id, user.Name, user.Email);
    }
}
```

### 2. Register Services

```csharp
// Program.cs or Startup.cs
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
});
```

### 3. Send Requests

```csharp
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserQuery(id), ct);
        return Ok(result);
    }
}
```

## Usage Patterns

### Commands (No Response)

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Create user logic
        await _repository.CreateAsync(new User(request.Name, request.Email), ct);
        return Unit.Value;
    }
}

// Usage
await _mediator.Send(new CreateUserCommand("John", "john@example.com"));
```

### Notifications (Pub/Sub)

Multiple handlers can respond to a single notification:

```csharp
public record UserCreated(int UserId, string Email) : INotification;

public class SendWelcomeEmailHandler : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken ct)
    {
        await _emailService.SendWelcomeAsync(notification.Email, ct);
    }
}

public class CreateAuditLogHandler : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken ct)
    {
        await _auditService.LogAsync($"User {notification.UserId} created", ct);
    }
}

// Both handlers execute in parallel
await _mediator.Publish(new UserCreated(123, "john@example.com"));
```

### Streaming (IAsyncEnumerable)

For large datasets or real-time data:

```csharp
public record GetLogsStream(DateTime From, DateTime To) : IStreamRequest<LogEntry>;

public class GetLogsHandler : IStreamRequestHandler<GetLogsStream, LogEntry>
{
    private readonly ILogRepository _repository;

    public async IAsyncEnumerable<LogEntry> Handle(
        GetLogsStream request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var log in _repository.StreamLogsAsync(request.From, request.To, ct))
        {
            yield return log;
        }
    }
}

// Usage - process items as they arrive
await foreach (var log in _mediator.CreateStream(new GetLogsStream(from, to), ct))
{
    Console.WriteLine($"[{log.Timestamp}] {log.Message}");
}
```

## Pipeline Behaviors

Pipeline behaviors wrap handler execution, enabling cross-cutting concerns:

```
Request → Behavior1 → Behavior2 → Handler → Behavior2 → Behavior1 → Response
```

### Built-in Behaviors

| Behavior | Purpose |
|----------|---------|
| `LoggingBehavior<,>` | Logs request start/end with cached type names |
| `ValidationBehavior<,>` | Validates requests using `IValidator<T>` |
| `PerformanceBehavior<,>` | Measures execution time, warns on slow requests (>500ms) |
| `ExceptionHandlingBehavior<,>` | Consistent error handling and logging |
| `RequestPreProcessorBehavior<,>` | Executes all `IRequestPreProcessor<T>` |
| `RequestPostProcessorBehavior<,>` | Executes all `IRequestPostProcessor<T,R>` |

### Registration

```csharp
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
    
    // Behaviors execute in registration order
    config.AddBehavior(typeof(LoggingBehavior<,>));
    config.AddBehavior(typeof(ValidationBehavior<,>));
    config.AddBehavior(typeof(PerformanceBehavior<,>));
});
```

### Custom Behavior

```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDbContext _dbContext;

    public TransactionBehavior(IDbContext dbContext) => _dbContext = dbContext;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        await using var transaction = await _dbContext.BeginTransactionAsync(ct);
        
        try
        {
            var response = await next();
            await transaction.CommitAsync(ct);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

## Validation

```csharp
public class CreateUserValidator : IValidator<CreateUserCommand>
{
    public Task<ValidationResult> ValidateAsync(CreateUserCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Name))
            failures.Add(new ValidationFailure("Name", "Name is required"));

        if (string.IsNullOrWhiteSpace(request.Email))
            failures.Add(new ValidationFailure("Email", "Email is required"));
        else if (!request.Email.Contains('@'))
            failures.Add(new ValidationFailure("Email", "Invalid email format"));

        return Task.FromResult(failures.Count == 0
            ? ValidationResult.Success
            : ValidationResult.WithFailures(failures));
    }
}

// Register validator and behavior
services.AddMediator(config =>
{
    config.AddAssembly(typeof(Program).Assembly);
    config.AddBehavior(typeof(ValidationBehavior<,>));
});
services.AddTransient<IValidator<CreateUserCommand>, CreateUserValidator>();
```

## Pre/Post Processors

Execute logic before or after handler:

```csharp
public class LoggingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger _logger;

    public Task Process(TRequest request, CancellationToken ct)
    {
        _logger.LogDebug("Processing {RequestType}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}

public class CachePostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICache _cache;

    public async Task Process(TRequest request, TResponse response, CancellationToken ct)
    {
        if (request is ICacheable cacheable)
        {
            await _cache.SetAsync(cacheable.CacheKey, response, ct);
        }
    }
}
```

## Configuration

```csharp
services.AddMediator(config =>
{
    // Scan multiple assemblies
    config.AddAssembly(typeof(Program).Assembly);
    config.AddAssembly(typeof(Domain.AssemblyMarker).Assembly);

    // Configure service lifetimes
    config.MediatorLifetime = ServiceLifetime.Scoped;     // default: Scoped
    config.HandlerLifetime = ServiceLifetime.Transient;   // default: Transient
    config.BehaviorLifetime = ServiceLifetime.Transient;  // default: Transient

    // Enable generic behavior registration
    config.RegisterGenericBehaviors = true;               // default: true

    // Add behaviors (order matters!)
    config.AddBehavior(typeof(LoggingBehavior<,>));
    config.AddBehavior(typeof(ValidationBehavior<,>));
    config.AddBehavior(typeof(RequestPreProcessorBehavior<,>));
    config.AddBehavior(typeof(RequestPostProcessorBehavior<,>));
});
```

## Performance Optimizations

This library is designed for high-throughput scenarios:

### Static Caching
- Handler wrappers are cached in `ConcurrentDictionary` and shared across all Mediator instances
- Type names are cached to avoid repeated reflection

### Minimal Allocations
- `Unit` is a `readonly struct` (stack-allocated)
- `Unit.Task` is a pre-cached completed task
- `ArrayPool<Task>` used for notification publishing (3+ handlers)
- `ObjectPool<Stopwatch>` in `PerformanceBehavior`

### Aggressive Inlining
- Hot paths marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- Separate non-inlined methods for exception paths

### Typed Handler Path
- Generic `Send<TResponse>` uses `ITypedRequestHandler<TResponse>` to avoid boxing

## Project Structure

```
Mediator/
├── src/Mediator/
│   ├── Abstractions/           # Core interfaces
│   │   ├── IMediator.cs        # Main facade (ISender + IPublisher)
│   │   ├── IRequest.cs         # Request marker interfaces
│   │   ├── IRequestHandler.cs  # Handler interface
│   │   ├── INotification.cs    # Notification marker
│   │   ├── INotificationHandler.cs
│   │   ├── IStreamRequest.cs   # Streaming support
│   │   ├── IPipelineBehavior.cs
│   │   └── ...
│   ├── Behaviors/              # Built-in pipeline behaviors
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   ├── PerformanceBehavior.cs
│   │   └── ...
│   ├── Validation/             # Validation framework
│   ├── Exceptions/             # Custom exceptions
│   ├── Wrappers/               # Internal handler wrappers
│   ├── Mediator.cs             # Main implementation
│   ├── Unit.cs                 # Unit type for void returns
│   └── ServiceCollectionExtensions.cs  # DI registration
├── tests/Mediator.Tests/       # 79 unit tests
└── benchmarks/Mediator.Benchmarks/     # Performance benchmarks
```

## API Reference

### Core Interfaces

| Interface | Description |
|-----------|-------------|
| `IMediator` | Main facade combining `ISender` and `IPublisher` |
| `ISender` | Sends requests to single handler |
| `IPublisher` | Publishes notifications to multiple handlers |
| `IRequest<TResponse>` | Marker for requests with response |
| `IRequest` | Shorthand for `IRequest<Unit>` |
| `IRequestHandler<TRequest, TResponse>` | Handler for requests |
| `INotification` | Marker for notifications |
| `INotificationHandler<TNotification>` | Handler for notifications |
| `IStreamRequest<TResponse>` | Stream request returning `IAsyncEnumerable` |
| `IStreamRequestHandler<TRequest, TResponse>` | Handler for stream requests |
| `IPipelineBehavior<TRequest, TResponse>` | Pipeline middleware |
| `IValidator<TRequest>` | Request validator |

## Testing

```bash
cd tests/Mediator.Tests
dotnet test
```

The test suite covers:
- Request/Response handling
- Notifications with multiple handlers
- Pipeline behaviors with ordering
- Pre/Post processors
- Validation
- Streaming with `IAsyncEnumerable`
- Cancellation support
- Exception handling
- DI registration

## Benchmarks

Run benchmarks:

```bash
cd benchmarks/Mediator.Benchmarks
dotnet run -c Release
```

## Requirements

- .NET 8.0 or later
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.1+
- Microsoft.Extensions.Logging.Abstractions 10.0.1+

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by [MediatR](https://github.com/jbogard/MediatR) by Jimmy Bogard
- Built with modern .NET best practices
