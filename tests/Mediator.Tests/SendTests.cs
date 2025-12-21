using FluentAssertions;
using Mediator.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for IMediator.Send functionality.
/// </summary>
public class SendTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public SendTests()
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(SendTests).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Reset static state
        VoidRequestHandler.Reset();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Send_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new PingRequest("Hello");

        // Act
        var response = await _mediator.Send(request);

        // Assert
        response.Should().NotBeNull();
        response.Reply.Should().Be("Pong: Hello");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Short")]
    [InlineData("A very long message with special characters @#$%^&*()")]
    [InlineData("Unicode: 日本語 한국어 中文")]
    public async Task Send_WithVariousMessages_ReturnsCorrectResponse(string message)
    {
        // Arrange
        var request = new PingRequest(message);

        // Act
        var response = await _mediator.Send(request);

        // Assert
        response.Should().NotBeNull();
        response.Reply.Should().Be($"Pong: {message}");
    }

    [Fact]
    public async Task Send_WithVoidRequest_ReturnsUnit()
    {
        // Arrange
        var request = new VoidRequest("test data");
        VoidRequestHandler.Reset();

        // Act
        var result = await _mediator.Send(request);

        // Assert
        result.Should().Be(Unit.Value);
        VoidRequestHandler.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task Send_WithDifferentMessages_ReturnsDifferentResponses()
    {
        // Arrange
        var request1 = new PingRequest("First");
        var request2 = new PingRequest("Second");

        // Act
        var response1 = await _mediator.Send(request1);
        var response2 = await _mediator.Send(request2);

        // Assert
        response1.Reply.Should().Be("Pong: First");
        response2.Reply.Should().Be("Pong: Second");
    }

    [Fact]
    public async Task Send_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _mediator.Send<PongResponse>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Send_WithFailingHandler_ThrowsException()
    {
        // Arrange
        var request = new FailingRequest("Test error");

        // Act
        Func<Task> act = async () => await _mediator.Send(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }

    [Fact]
    public async Task Send_WithCancellationToken_PassesTokenToHandler()
    {
        // Arrange
        var request = new PingRequest("Test");
        using var cts = new CancellationTokenSource();

        // Act
        var response = await _mediator.Send(request, cts.Token);

        // Assert
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task Send_ObjectOverload_ReturnsCorrectResponse()
    {
        // Arrange
        object request = new PingRequest("Object test");

        // Act
        var response = await _mediator.Send(request);

        // Assert
        response.Should().NotBeNull();
        response.Should().BeOfType<PongResponse>();
        ((PongResponse)response!).Reply.Should().Be("Pong: Object test");
    }

    [Fact]
    public async Task Send_MultipleRequests_AllHandledCorrectly()
    {
        // Arrange
        var requests = Enumerable.Range(1, 10)
            .Select(i => new PingRequest($"Message {i}"))
            .ToList();

        // Act
        var responses = new List<PongResponse>();
        foreach (var request in requests)
        {
            responses.Add(await _mediator.Send(request));
        }

        // Assert
        responses.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            responses[i].Reply.Should().Be($"Pong: Message {i + 1}");
        }
    }

    [Fact]
    public async Task Send_ConcurrentRequests_AllHandledCorrectly()
    {
        // Arrange
        var requests = Enumerable.Range(1, 100)
            .Select(i => new PingRequest($"Concurrent {i}"))
            .ToList();

        // Act
        var tasks = requests.Select(r => _mediator.Send(r));
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(100);
        responses.Should().AllSatisfy(r => r.Reply.Should().StartWith("Pong: Concurrent"));
    }
}
