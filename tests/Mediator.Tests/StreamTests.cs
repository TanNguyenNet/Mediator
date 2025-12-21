using FluentAssertions;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for IStreamRequest and IStreamRequestHandler functionality.
/// </summary>
public class StreamTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public StreamTests()
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(StreamTests).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public void StreamRequestHandler_IsRegistered()
    {
        // Act
        var handler = _serviceProvider.GetService<IStreamRequestHandler<NumberStreamRequest, int>>();

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<NumberStreamHandler>();
    }

    [Fact]
    public async Task StreamRequestHandler_ReturnsAsyncEnumerable()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<NumberStreamRequest, int>>();
        var request = new NumberStreamRequest(5);

        // Act
        var items = new List<int>();
        await foreach (var item in handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        items.Should().HaveCount(5);
        items.Should().ContainInOrder(0, 1, 2, 3, 4);
    }

    [Fact]
    public async Task StreamRequestHandler_WithCancellation_StopsEarly()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<NumberStreamRequest, int>>();
        var request = new NumberStreamRequest(100);
        using var cts = new CancellationTokenSource();

        // Act
        var items = new List<int>();
        await foreach (var item in handler.Handle(request, cts.Token))
        {
            items.Add(item);
            if (items.Count >= 3)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert
        items.Should().HaveCount(3);
        items.Should().ContainInOrder(0, 1, 2);
    }

    [Fact]
    public async Task StreamRequestHandler_EmptyStream_ReturnsNoItems()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<NumberStreamRequest, int>>();
        var request = new NumberStreamRequest(0);

        // Act
        var items = new List<int>();
        await foreach (var item in handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task StreamRequestHandler_WithDelay_YieldsItemsAsynchronously()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<DelayedStreamRequest, string>>();
        var request = new DelayedStreamRequest(3, 10);

        // Act
        var items = new List<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await foreach (var item in handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }
        sw.Stop();

        // Assert
        items.Should().HaveCount(3);
        items.Should().ContainInOrder("Item 0", "Item 1", "Item 2");
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(20); // At least 2 delays
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task StreamRequestHandler_VariousCounts_ReturnsCorrectNumber(int count)
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<NumberStreamRequest, int>>();
        var request = new NumberStreamRequest(count);

        // Act
        var items = new List<int>();
        await foreach (var item in handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        items.Should().HaveCount(count);
    }

    [Fact]
    public async Task StreamRequestHandler_CompositeStream_YieldsAllItems()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<CompositeStreamRequest, object>>();
        var request = new CompositeStreamRequest();

        // Act
        var items = new List<object>();
        await foreach (var item in handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        // Assert
        items.Should().HaveCount(3);
        items[0].Should().BeOfType<int>();
        items[1].Should().BeOfType<string>();
        items[2].Should().BeOfType<decimal>();
    }
}

// ========== STREAM REQUESTS ==========

/// <summary>
/// Stream request that yields a sequence of numbers.
/// </summary>
public record NumberStreamRequest(int Count) : IStreamRequest<int>;

/// <summary>
/// Stream request with delay between yields.
/// </summary>
public record DelayedStreamRequest(int Count, int DelayMs) : IStreamRequest<string>;

/// <summary>
/// Stream request that yields mixed types.
/// </summary>
public record CompositeStreamRequest() : IStreamRequest<object>;

// ========== STREAM HANDLERS ==========

/// <summary>
/// Handler for NumberStreamRequest.
/// </summary>
public class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        NumberStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield(); // Allow async behavior
        }
    }
}

/// <summary>
/// Handler for DelayedStreamRequest.
/// </summary>
public class DelayedStreamHandler : IStreamRequestHandler<DelayedStreamRequest, string>
{
    public async IAsyncEnumerable<string> Handle(
        DelayedStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return $"Item {i}";
            await Task.Delay(request.DelayMs, cancellationToken);
        }
    }
}

/// <summary>
/// Handler for CompositeStreamRequest.
/// </summary>
public class CompositeStreamHandler : IStreamRequestHandler<CompositeStreamRequest, object>
{
    public async IAsyncEnumerable<object> Handle(
        CompositeStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 42;
        await Task.Yield();
        yield return "Hello";
        await Task.Yield();
        yield return 3.14m;
    }
}
