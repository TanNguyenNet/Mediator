using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;

// Use aliases to avoid namespace conflicts
using OurMediator = global::Mediator;
using MediatRLib = global::MediatR;

namespace Mediator.Benchmarks;

/// <summary>
/// Benchmarks comparing our Mediator library with MediatR.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MediatorBenchmarks
{
    private IServiceProvider _ourMediatorProvider = null!;
    private IServiceProvider _mediatRProvider = null!;
    
    private OurMediator.IMediator _ourMediator = null!;
    private MediatRLib.IMediator _mediatR = null!;
    
    private OurPingRequest _ourRequest = null!;
    private MediatRPingRequest _mediatRRequest = null!;
    
    private OurNotification _ourNotification = null!;
    private MediatRNotification _mediatRNotification = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup our Mediator
        var ourServices = new ServiceCollection();
        ourServices.AddMediator(config =>
        {
            config.AddAssembly(typeof(MediatorBenchmarks).Assembly);
        });
        _ourMediatorProvider = ourServices.BuildServiceProvider();
        _ourMediator = _ourMediatorProvider.GetRequiredService<OurMediator.IMediator>();
        
        // Setup MediatR
        var mediatRServices = new ServiceCollection();
        mediatRServices.AddMediatR(cfg => 
            cfg.RegisterServicesFromAssembly(typeof(MediatorBenchmarks).Assembly));
        _mediatRProvider = mediatRServices.BuildServiceProvider();
        _mediatR = _mediatRProvider.GetRequiredService<MediatRLib.IMediator>();
        
        // Create requests
        _ourRequest = new OurPingRequest("Benchmark Test");
        _mediatRRequest = new MediatRPingRequest("Benchmark Test");
        
        _ourNotification = new OurNotification("Benchmark");
        _mediatRNotification = new MediatRNotification("Benchmark");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_ourMediatorProvider as IDisposable)?.Dispose();
        (_mediatRProvider as IDisposable)?.Dispose();
    }

    // ===== REQUEST/RESPONSE BENCHMARKS =====
    
    [Benchmark(Description = "Our Mediator - Send Request")]
    public async Task<OurPongResponse> OurMediator_Send()
    {
        return await _ourMediator.Send(_ourRequest);
    }

    [Benchmark(Baseline = true, Description = "MediatR - Send Request")]
    public async Task<MediatRPongResponse> MediatR_Send()
    {
        return await _mediatR.Send(_mediatRRequest);
    }

    // ===== NOTIFICATION BENCHMARKS =====
    
    [Benchmark(Description = "Our Mediator - Publish Notification")]
    public async Task OurMediator_Publish()
    {
        await _ourMediator.Publish(_ourNotification);
    }

    [Benchmark(Description = "MediatR - Publish Notification")]
    public async Task MediatR_Publish()
    {
        await _mediatR.Publish(_mediatRNotification);
    }

    // ===== MULTIPLE REQUESTS BENCHMARKS =====
    
    [Benchmark(Description = "Our Mediator - 100 Requests")]
    public async Task OurMediator_100Requests()
    {
        for (int i = 0; i < 100; i++)
        {
            await _ourMediator.Send(new OurPingRequest($"Request {i}"));
        }
    }

    [Benchmark(Description = "MediatR - 100 Requests")]
    public async Task MediatR_100Requests()
    {
        for (int i = 0; i < 100; i++)
        {
            await _mediatR.Send(new MediatRPingRequest($"Request {i}"));
        }
    }

    // ===== CONCURRENT REQUESTS BENCHMARKS =====
    
    [Benchmark(Description = "Our Mediator - 100 Concurrent")]
    public async Task OurMediator_Concurrent()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(i => _ourMediator.Send(new OurPingRequest($"Concurrent {i}")));
        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "MediatR - 100 Concurrent")]
    public async Task MediatR_Concurrent()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(i => _mediatR.Send(new MediatRPingRequest($"Concurrent {i}")));
        await Task.WhenAll(tasks);
    }
}

// =============================================
// OUR MEDIATOR TYPES
// =============================================

public record OurPingRequest(string Message) : OurMediator.IRequest<OurPongResponse>;
public record OurPongResponse(string Reply);

public class OurPingHandler : OurMediator.IRequestHandler<OurPingRequest, OurPongResponse>
{
    public Task<OurPongResponse> Handle(OurPingRequest request, CancellationToken ct)
    {
        return Task.FromResult(new OurPongResponse($"Pong: {request.Message}"));
    }
}

public record OurNotification(string Message) : OurMediator.INotification;

public class OurNotificationHandler1 : OurMediator.INotificationHandler<OurNotification>
{
    public Task Handle(OurNotification notification, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

public class OurNotificationHandler2 : OurMediator.INotificationHandler<OurNotification>
{
    public Task Handle(OurNotification notification, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

// =============================================
// MEDIATR TYPES
// =============================================

public record MediatRPingRequest(string Message) : MediatRLib.IRequest<MediatRPongResponse>;
public record MediatRPongResponse(string Reply);

public class MediatRPingHandler : MediatRLib.IRequestHandler<MediatRPingRequest, MediatRPongResponse>
{
    public Task<MediatRPongResponse> Handle(MediatRPingRequest request, CancellationToken ct)
    {
        return Task.FromResult(new MediatRPongResponse($"Pong: {request.Message}"));
    }
}

public record MediatRNotification(string Message) : MediatRLib.INotification;

public class MediatRNotificationHandler1 : MediatRLib.INotificationHandler<MediatRNotification>
{
    public Task Handle(MediatRNotification notification, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

public class MediatRNotificationHandler2 : MediatRLib.INotificationHandler<MediatRNotification>
{
    public Task Handle(MediatRNotification notification, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
