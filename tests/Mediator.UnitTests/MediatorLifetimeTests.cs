using Atya.Foundation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.UnitTests;

public sealed class MediatorLifetimeTests
{
    [Fact]
    public async Task ResolvingMediator_ConstructsNoHandlers_AndOneSendConstructsExactlyOne()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Transient);
        await using ServiceProvider _ = provider;
        using IServiceScope scope = provider.CreateScope();

        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        probe.Constructed.Should().Be(0);

        await mediator.Send(new Ping(), TestContext.Current.CancellationToken);

        probe.Constructed.Should().Be(1);
    }

    [Fact]
    public async Task TransientHandler_IsConstructedPerSend()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Transient);
        await using ServiceProvider _ = provider;
        using IServiceScope scope = provider.CreateScope();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        await mediator.Send(new Ping(), TestContext.Current.CancellationToken);

        probe.Constructed.Should().Be(2);
    }

    [Fact]
    public async Task ScopedHandler_ReceivesTheAmbientScopedDependency()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Scoped);
        await using ServiceProvider _ = provider;
        using IServiceScope scope = provider.CreateScope();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new Ping(), TestContext.Current.CancellationToken);

        probe.ObservedDependency.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<ScopedDependency>());
    }

    [Fact]
    public async Task ScopedHandler_IsDisposedWhenTheScopeEnds_NotWhenSendReturns()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Scoped);
        await using ServiceProvider _ = provider;
        IServiceScope scope = provider.CreateScope();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new Ping(), TestContext.Current.CancellationToken);

        probe.Events.Should().Equal("Constructed");

        scope.Dispose();

        probe.Events.Should().Equal("Constructed", "Disposed");
    }

    [Fact]
    public async Task TwoInterfaceScopedHandler_IsOneInstancePerScopeAcrossBothSendTypes()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Scoped);
        await using ServiceProvider _ = provider;

        using (IServiceScope first = provider.CreateScope())
        {
            IMediator mediator = first.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
            await mediator.Send(new Echo("x"), TestContext.Current.CancellationToken);
            await mediator.Send(new Ping(), TestContext.Current.CancellationToken);
        }

        probe.Constructed.Should().Be(1);

        using (IServiceScope second = provider.CreateScope())
        {
            await second.ServiceProvider.GetRequiredService<IMediator>().Send(new Echo("y"), TestContext.Current.CancellationToken);
        }

        probe.Constructed.Should().Be(2);
    }

    [Fact]
    public async Task SingletonHandler_IsSharedAcrossScopes()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Singleton, scopedDependency: false);
        await using ServiceProvider _ = provider;

        for (int i = 0; i < 3; i++)
        {
            using IServiceScope scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new Ping(), TestContext.Current.CancellationToken);
        }

        probe.Constructed.Should().Be(1);
    }

    [Theory]
    [InlineData(MediatorHandlerLifetime.Transient)]
    [InlineData(MediatorHandlerLifetime.Scoped)]
    public void ScopeAndBuildValidation_Succeeds(MediatorHandlerLifetime lifetime)
    {
        ServiceCollection services = new();
        services.AddSingleton(new Probe());
        services.AddScoped<ScopedDependency>();
        services.AddAtyaMediator(builder =>
        {
            builder.AddRequestHandler<Ping, MultiHandler>(lifetime);
            builder.AddRequestHandler<Echo, string, MultiHandler>(lifetime);
        });

        Func<ServiceProvider> build = () => services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        build.Should().NotThrow();
    }

    [Fact]
    public async Task SendThroughRootProviderMediator_StillWorks()
    {
        (ServiceProvider provider, Probe probe) = Build(MediatorHandlerLifetime.Transient);
        await using ServiceProvider _ = provider;

        Result<string> result = await provider.GetRequiredService<IMediator>().Send(new Echo("root"), TestContext.Current.CancellationToken);

        result.Value.Should().Be("root");
        probe.Constructed.Should().Be(1);
    }

    private static (ServiceProvider Provider, Probe Probe) Build(MediatorHandlerLifetime lifetime, bool scopedDependency = true)
    {
        var probe = new Probe();
        ServiceCollection services = new();
        services.AddSingleton(probe);
        if (scopedDependency)
        {
            services.AddScoped<ScopedDependency>();
        }

        services.AddAtyaMediator(builder =>
        {
            builder.AddRequestHandler<Ping, MultiHandler>(lifetime);
            builder.AddRequestHandler<Echo, string, MultiHandler>(lifetime);
        });

        return (services.BuildServiceProvider(), probe);
    }

    private sealed record class Ping : IRequest;

    private sealed record class Echo(string Value) : IRequest<string>;

    private sealed class ScopedDependency
    {
    }

    private sealed class Probe
    {
        public int Constructed { get; set; }

        public ScopedDependency? ObservedDependency { get; set; }

        public List<string> Events { get; } = new();
    }

    private sealed class MultiHandler : IRequestHandler<Ping>, IRequestHandler<Echo, string>, IDisposable
    {
        private readonly Probe _probe;

        public MultiHandler(Probe probe, IServiceProvider services)
        {
            _probe = probe;
            _probe.Constructed++;
            _probe.Events.Add("Constructed");
            _probe.ObservedDependency = services.GetService<ScopedDependency>();
        }

        public ValueTask<Result> Handle(Ping request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result<string>> Handle(Echo request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success(request.Value));

        public void Dispose() => _probe.Events.Add("Disposed");
    }
}
