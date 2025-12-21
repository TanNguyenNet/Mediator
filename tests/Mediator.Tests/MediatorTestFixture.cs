using Microsoft.Extensions.DependencyInjection;

namespace Mediator.Tests;

/// <summary>
/// Shared test fixture for Mediator tests.
/// Provides a pre-configured IMediator instance to reduce setup duplication.
/// </summary>
public class MediatorTestFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public IMediator Mediator { get; }

    public MediatorTestFixture()
    {
        var services = new ServiceCollection();
        services.AddMediator(config =>
        {
            config.AddAssembly(typeof(MediatorTestFixture).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
        Mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
