# Kế Hoạch Xây Dựng Thư Viện MediatR - C#

## 1. Tổng Quan

MediatR là một thư viện triển khai **Mediator Pattern** kết hợp với **CQRS (Command Query Responsibility Segregation)** trong .NET. Thư viện này giúp tách biệt việc gửi request và xử lý request, tạo ra mã nguồn sạch và dễ bảo trì.

> [!IMPORTANT]
> **Mục tiêu chính**: Ưu tiên **PERFORMANCE** và **ZERO MEMORY LEAK**
> - Sử dụng `Task` với cached completions
> - Object Pooling cho handlers và wrappers
> - Cache handler mappings để tránh reflection lặp lại
> - Avoid boxing/unboxing với generics
> - Sử dụng `struct` cho Unit và các value types
> - `ConcurrentDictionary` với lazy initialization

### Kiến trúc tổng quan

```
┌─────────────┐         ┌───────────┐         ┌─────────────────┐
│  Controller │ ──────► │  MediatR  │ ──────► │ Request Handler │
└─────────────┘         └───────────┘         └─────────────────┘
                              │
                              ▼
                    ┌───────────────────┐
                    │ Pipeline Behaviors│
                    │  (Pre/Post)       │
                    └───────────────────┘
```

---

## 2. 🚀 Performance & Memory Optimization Strategy

### 2.1 Chiến lược chống Memory Leak

| Vấn đề | Giải pháp |
|--------|-----------|
| Handler không được dispose | Sử dụng `IServiceScope` đúng cách, dispose sau mỗi request |
| Closure capturing | Tránh lambda capture biến ngoài, sử dụng static lambda |
| Event handler không unsubscribe | Không sử dụng event, dùng delegate trực tiếp |
| Reflection cache vô hạn | Sử dụng `ConditionalWeakTable` hoặc bounded cache |
| Large object heap fragmentation | Object pooling với `ArrayPool<T>` và `ObjectPool<T>` |

### 2.2 Kỹ thuật tối ưu Performance

```csharp
// ❌ TRÁNH: Allocate mỗi lần gọi
public Task<TResponse> Send<TResponse>(IRequest<TResponse> request) 
{
    var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
    // Reflection mỗi lần gọi = CHẬM
}

// ✅ SỬ DỤNG: Cache handler factory
private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _handlerCache = new();

public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
{
    var handler = _handlerCache.GetOrAdd(
        request.GetType(), 
        static t => CreateHandler(t)); // static lambda - no allocation
    
    return handler.Handle(request, _serviceProvider, ct);
}
```

### 2.3 Task với Cached Completions

```csharp
// ✅ Sử dụng Task với caching cho Unit responses
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Tối ưu với cached Task:
// - Unit.Task được cache sẵn, không allocate mới
// - Task.FromResult<T> cho các giá trị thường xuyên sử dụng
// - Sử dụng cached Task completions trong handlers
```

### 2.4 Object Pooling Strategy

```csharp
// Sử dụng ObjectPool cho wrappers
public sealed class RequestHandlerWrapperPool<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ObjectPool<RequestHandlerWrapper<TRequest, TResponse>> _pool =
        new DefaultObjectPoolProvider().Create<RequestHandlerWrapper<TRequest, TResponse>>();

    public static RequestHandlerWrapper<TRequest, TResponse> Get() => _pool.Get();
    public static void Return(RequestHandlerWrapper<TRequest, TResponse> wrapper) => _pool.Return(wrapper);
}
```

### 2.5 Span<T> và Memory<T> cho Collections

```csharp
// ❌ TRÁNH: LINQ tạo nhiều allocations
var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>().ToList();

// ✅ SỬ DỤNG: Pre-allocated array với ArrayPool
var array = ArrayPool<INotificationHandler<TNotification>>.Shared.Rent(expectedCount);
try
{
    int count = 0;
    foreach (var handler in _serviceProvider.GetServices<INotificationHandler<TNotification>>())
    {
        array[count++] = handler;
    }
    // Process handlers...
}
finally
{
    ArrayPool<INotificationHandler<TNotification>>.Shared.Return(array);
}
```

---

## 3. Core Interfaces

### 2.1 Request Interfaces

| Interface | Mô tả |
|-----------|-------|
| `IRequest<TResponse>` | Request có response |
| `IRequest` | Request không có response (void) |
| `INotification` | Notification cho publish/subscribe pattern |

### 2.2 Handler Interfaces

| Interface | Mô tả |
|-----------|-------|
| `IRequestHandler<TRequest, TResponse>` | Handler xử lý request có response |
| `IRequestHandler<TRequest>` | Handler xử lý request không có response |
| `INotificationHandler<TNotification>` | Handler xử lý notification |

### 2.3 Mediator Interface

| Interface | Mô tả |
|-----------|-------|
| `IMediator` | Interface chính để gửi request/notification |
| `ISender` | Interface gửi request (Send) |
| `IPublisher` | Interface publish notification |

### 2.4 Pipeline Behavior

| Interface | Mô tả |
|-----------|-------|
| `IPipelineBehavior<TRequest, TResponse>` | Middleware cho request pipeline |

---

## 3. Chi Tiết Triển Khai

### Phase 1: Core Abstractions
> **Mục tiêu**: Tạo các interface và abstract class cơ bản

#### Files cần tạo:

```
src/
├── Mediator/
│   ├── Abstractions/
│   │   ├── IRequest.cs
│   │   ├── IRequestHandler.cs
│   │   ├── INotification.cs
│   │   ├── INotificationHandler.cs
│   │   ├── IMediator.cs
│   │   ├── ISender.cs
│   │   ├── IPublisher.cs
│   │   └── IPipelineBehavior.cs
│   └── Mediator.csproj
```

#### Chi tiết Interface (Performance-Optimized):

> [!TIP]
> Sử dụng `Task` với cached completions và static caching để tối ưu performance

```csharp
// IRequest.cs - Marker interface, zero allocation
public interface IRequest<out TResponse> { }
public interface IRequest : IRequest<Unit> { }

// IBaseRequest.cs - Base interface for covariant scenarios
public interface IBaseRequest { }

// IRequestHandler.cs - Standard Task for async operations
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// INotification.cs - Marker interface
public interface INotification { }

// INotificationHandler.cs - Task for async notification handling
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

// IMediator.cs - Composite interface
public interface IMediator : ISender, IPublisher { }

// ISender.cs - Task-based request sending
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task<object?> Send(object request, CancellationToken cancellationToken = default);
}

// IPublisher.cs - Task-based notification publishing
public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
    Task Publish(object notification, CancellationToken cancellationToken = default);
}

// IPipelineBehavior.cs - Pipeline middleware with Task
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

// Delegate sử dụng Task
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
```

---

### Phase 2: Core Implementation
> **Mục tiêu**: Triển khai Mediator class và các helper

#### Files cần tạo:

```
src/
├── Mediator/
│   ├── Implementations/
│   │   ├── Mediator.cs
│   │   └── Unit.cs
│   ├── Wrappers/
│   │   ├── RequestHandlerWrapper.cs
│   │   └── NotificationHandlerWrapper.cs
```

#### Chi tiết Implementation (High-Performance):

```csharp
// Unit.cs - Readonly struct để tránh boxing, cached Task
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    // Singleton pattern với static readonly
    public static readonly Unit Value = new();
    
    // ⚡ Cached Task để tái sử dụng, zero allocation
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
    
    // IEquatable<Unit> implementation - inline để tránh virtual call
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other) => true;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is Unit;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Unit other) => 0;
    
    public override string ToString() => "()";
    
    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}

// Mediator.cs - High-performance implementation
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;
    
    // ⚡ Static cache - shared across all instances, thread-safe
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlerCache = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlerCache = new();

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        // Cache lookup với static lambda để avoid closure allocation
        var handler = _requestHandlerCache.GetOrAdd(
            request.GetType(),
            static requestType => CreateRequestHandler(requestType));
        
        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var handler = _requestHandlerCache.GetOrAdd(
            request.GetType(),
            static requestType => CreateRequestHandler(requestType));
        
        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishCore(notification, cancellationToken);
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishCore(notification, cancellationToken);
    }

    // Private helper để tối ưu code path
    private async Task PublishCore(object notification, CancellationToken cancellationToken)
    {
        var wrapper = _notificationHandlerCache.GetOrAdd(
            notification.GetType(),
            static notificationType => CreateNotificationHandler(notificationType));
        
        await wrapper.Handle(notification, _serviceProvider, cancellationToken);
    }

    // Factory methods - chỉ gọi một lần, sau đó cached
    private static RequestHandlerBase CreateRequestHandler(Type requestType) => /* reflection once */;
    private static NotificationHandlerWrapper CreateNotificationHandler(Type notificationType) => /* reflection once */;
}
```

---

### Phase 3: Dependency Injection Extensions
> **Mục tiêu**: Tích hợp với Microsoft.Extensions.DependencyInjection

#### Files cần tạo:

```
src/
├── Mediator.Extensions.DependencyInjection/
│   ├── ServiceCollectionExtensions.cs
│   ├── MediatorServiceConfiguration.cs
│   └── Mediator.Extensions.DependencyInjection.csproj
```

#### Chi tiết:

```csharp
// ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Auto-register all handlers từ assemblies
        // Register Mediator
        // Register Pipeline behaviors
    }

    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorServiceConfiguration> configure,
        params Assembly[] assemblies)
    {
        // Cấu hình nâng cao
    }
}

// MediatorServiceConfiguration.cs
public class MediatorServiceConfiguration
{
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);
}
```

---

### Phase 4: Built-in Pipeline Behaviors
> **Mục tiêu**: Cung cấp các behavior thông dụng

#### Files cần tạo:

```
src/
├── Mediator/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   ├── PerformanceBehavior.cs
│   │   └── ExceptionHandlingBehavior.cs
```

#### Chi tiết (Performance-Optimized Behaviors):

```csharp
// LoggingBehavior.cs - Cached type name để tránh reflection mỗi lần
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // ⚡ Cached type name - computed once per generic instantiation
    private static readonly string RequestTypeName = typeof(TRequest).Name;
    
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Sử dụng cached type name thay vì typeof().Name mỗi lần
        _logger.LogInformation("Handling {RequestName}", RequestTypeName);
        
        var response = await next();
        
        _logger.LogInformation("Handled {RequestName}", RequestTypeName);
        return response;
    }
}

// ValidationBehavior.cs - Early return nếu không có validators
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IValidator<TRequest>[] _validators; // Array thay vì IEnumerable để tránh allocation

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        // Materialize một lần, không enumerate lại
        _validators = validators.ToArray();
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // ⚡ Fast path: skip validation nếu không có validators
        if (_validators.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        
        // Parallel validation cho performance
        var validationTasks = new Task<ValidationResult>[_validators.Length];
        for (int i = 0; i < _validators.Length; i++)
        {
            validationTasks[i] = _validators[i].ValidateAsync(context, cancellationToken);
        }

        // Await all validation tasks
        await Task.WhenAll(validationTasks);

        // Collect failures
        var failures = new List<ValidationFailure>();
        for (int i = 0; i < validationTasks.Length; i++)
        {
            var result = validationTasks[i].Result;
            if (result.Errors.Count > 0)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}

// PerformanceBehavior.cs - Stopwatch pooling
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string RequestTypeName = typeof(TRequest).Name;
    private static readonly ObjectPool<Stopwatch> StopwatchPool = 
        new DefaultObjectPoolProvider().Create<StopwatchPooledObjectPolicy>();
    
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly long _thresholdMs;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = StopwatchPool.Get();
        try
        {
            stopwatch.Restart();
            var response = await next();
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > _thresholdMs)
            {
                _logger.LogWarning(
                    "Long running request: {RequestName} ({ElapsedMs}ms)",
                    RequestTypeName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        finally
        {
            stopwatch.Reset();
            StopwatchPool.Return(stopwatch);
        }
    }
}
```

---

### Phase 5: Stream Requests (Advanced)
> **Mục tiêu**: Hỗ trợ streaming với IAsyncEnumerable

#### Files cần tạo:

```
src/
├── Mediator/
│   ├── Abstractions/
│   │   ├── IStreamRequest.cs
│   │   └── IStreamRequestHandler.cs
```

#### Chi tiết:

```csharp
// IStreamRequest.cs
public interface IStreamRequest<out TResponse> { }

// IStreamRequestHandler.cs
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
```

---

## 4. CQRS Pattern Integration

### Command (Write Operations)

```csharp
// CreateOrderCommand.cs
public record CreateOrderCommand(string ProductName, int Quantity) : IRequest<OrderDto>;

// CreateOrderCommandHandler.cs
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Write to database
    }
}
```

### Query (Read Operations)

```csharp
// GetOrderQuery.cs
public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto>;

// GetOrderQueryHandler.cs
public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        // Read from database
    }
}
```

---

## 5. Project Structure

```
Mediator/
├── src/
│   ├── Mediator/
│   │   ├── Abstractions/
│   │   │   ├── IRequest.cs
│   │   │   ├── IRequestHandler.cs
│   │   │   ├── INotification.cs
│   │   │   ├── INotificationHandler.cs
│   │   │   ├── IMediator.cs
│   │   │   ├── ISender.cs
│   │   │   ├── IPublisher.cs
│   │   │   ├── IPipelineBehavior.cs
│   │   │   ├── IStreamRequest.cs
│   │   │   └── IStreamRequestHandler.cs
│   │   ├── Implementations/
│   │   │   ├── Mediator.cs
│   │   │   └── Unit.cs
│   │   ├── Wrappers/
│   │   │   ├── RequestHandlerWrapper.cs
│   │   │   ├── RequestHandlerWrapperImpl.cs
│   │   │   ├── NotificationHandlerWrapper.cs
│   │   │   └── NotificationHandlerWrapperImpl.cs
│   │   ├── Behaviors/
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── PerformanceBehavior.cs
│   │   │   └── ExceptionHandlingBehavior.cs
│   │   ├── Exceptions/
│   │   │   └── ValidationException.cs
│   │   └── Mediator.csproj
│   └── Mediator.Extensions.DependencyInjection/
│       ├── ServiceCollectionExtensions.cs
│       ├── MediatorServiceConfiguration.cs
│       └── Mediator.Extensions.DependencyInjection.csproj
├── tests/
│   ├── Mediator.Tests/
│   │   ├── SendTests.cs
│   │   ├── PublishTests.cs
│   │   ├── PipelineBehaviorTests.cs
│   │   └── Mediator.Tests.csproj
│   └── Mediator.Examples/
│       ├── Commands/
│       ├── Queries/
│       ├── Notifications/
│       └── Mediator.Examples.csproj
├── MediatR.sln
└── README.md
```

---

## 6. Timeline và Milestones

| Phase | Tên | Thời gian ước tính | Độ ưu tiên |
|-------|-----|-------------------|------------|
| 1 | Core Abstractions | 1 giờ | 🔴 Cao |
| 2 | Core Implementation | 2-3 giờ | 🔴 Cao |
| 3 | DI Extensions | 1-2 giờ | 🔴 Cao |
| 4 | Pipeline Behaviors | 1-2 giờ | 🟡 Trung bình |
| 5 | Stream Requests | 1 giờ | 🟢 Thấp |
| - | Tests | 2 giờ | 🟡 Trung bình |
| - | Documentation | 1 giờ | 🟡 Trung bình |

---

## 7. Dependencies

```xml
<!-- Mediator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>

<!-- Mediator.Extensions.DependencyInjection.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
    <ProjectReference Include="..\Mediator\Mediator.csproj" />
  </ItemGroup>
</Project>
```

---

## 8. Usage Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Đăng ký Mediator và tất cả handlers
builder.Services.AddMediator(typeof(Program).Assembly);

var app = builder.Build();

// Controller
app.MapPost("/orders", async (CreateOrderCommand command, IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return Results.Ok(result);
});

app.MapGet("/orders/{id}", async (Guid id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetOrderQuery(id));
    return Results.Ok(result);
});

app.Run();
```

---

## 9. Checklist Triển Khai

- [ ] Phase 1: Core Abstractions
  - [ ] IRequest.cs
  - [ ] IRequestHandler.cs
  - [ ] INotification.cs
  - [ ] INotificationHandler.cs
  - [ ] IMediator.cs, ISender.cs, IPublisher.cs
  - [ ] IPipelineBehavior.cs
- [ ] Phase 2: Core Implementation
  - [ ] Unit.cs
  - [ ] Mediator.cs
  - [ ] Request/Notification Wrappers
- [ ] Phase 3: DI Extensions
  - [ ] ServiceCollectionExtensions.cs
  - [ ] Auto-registration logic
- [ ] Phase 4: Pipeline Behaviors
  - [ ] LoggingBehavior
  - [ ] ValidationBehavior
  - [ ] PerformanceBehavior
- [ ] Phase 5: Stream Requests
  - [ ] IStreamRequest.cs
  - [ ] IStreamRequestHandler.cs
- [ ] Tests & Documentation

---

> [!NOTE]
> Kế hoạch này có thể được điều chỉnh dựa trên yêu cầu cụ thể. Hãy cho tôi biết nếu bạn muốn bắt đầu triển khai hoặc cần thay đổi gì trong kế hoạch này.
