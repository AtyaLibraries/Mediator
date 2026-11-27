using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.UnitTests;

public sealed class MediatorRegistrationBuilderTests
{
    [Fact]
    public void Constructor_NullServices_Throws()
    {
        Action act = () => _ = new MediatorRegistrationBuilder(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddAtyaMediator_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Action act = () => services.AddAtyaMediator(_ => { });

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddAtyaMediator_NullConfigure_Throws()
    {
        ServiceCollection services = new();

        Action act = () => services.AddAtyaMediator(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void AddRequestHandler_DuplicateUntypedRequest_Throws()
    {
        MediatorRegistrationBuilder builder = new(new ServiceCollection());
        builder.AddRequestHandler<PingCommand, PingCommandHandler>();

        Action act = () => builder.AddRequestHandler<PingCommand, SecondPingCommandHandler>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PingCommand*");
    }

    [Fact]
    public void AddRequestHandler_DuplicateTypedRequest_Throws()
    {
        MediatorRegistrationBuilder builder = new(new ServiceCollection());
        builder.AddRequestHandler<EchoQuery, string, EchoQueryHandler>();

        Action act = () => builder.AddRequestHandler<EchoQuery, string, SecondEchoQueryHandler>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*EchoQuery*");
    }

    [Fact]
    public void AddAtyaMediator_DuplicateAcrossCalls_ThrowsWhenMediatorIsResolved()
    {
        ServiceCollection services = new();
        services.AddAtyaMediator(builder => builder.AddRequestHandler<PingCommand, PingCommandHandler>());
        services.AddAtyaMediator(builder => builder.AddRequestHandler<PingCommand, SecondPingCommandHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Action act = () => _ = scope.ServiceProvider.GetRequiredService<IMediator>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PingCommand*");
    }

    [Fact]
    public void AddAtyaMediator_DuplicateTypedAcrossCalls_ThrowsWhenMediatorIsResolved()
    {
        ServiceCollection services = new();
        services.AddAtyaMediator(builder => builder.AddRequestHandler<EchoQuery, string, EchoQueryHandler>());
        services.AddAtyaMediator(builder => builder.AddRequestHandler<EchoQuery, string, SecondEchoQueryHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Action act = () => _ = scope.ServiceProvider.GetRequiredService<IMediator>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*EchoQuery*");
    }

    [Fact]
    public void AddRequestHandler_SameHandlerTwoLifetimes_Throws()
    {
        MediatorRegistrationBuilder builder = new(new ServiceCollection());
        builder.AddRequestHandler<PingCommand, TwoInterfaceHandler>(MediatorHandlerLifetime.Scoped);

        Action act = () => builder.AddRequestHandler<EchoQuery, string, TwoInterfaceHandler>(MediatorHandlerLifetime.Transient);

        act.Should().Throw<InvalidOperationException>().WithMessage("*TwoInterfaceHandler*one lifetime*");
    }

    [Fact]
    public void AddRequestHandler_UnknownLifetime_Throws()
    {
        MediatorRegistrationBuilder builder = new(new ServiceCollection());

        Action untyped = () => builder.AddRequestHandler<PingCommand, PingCommandHandler>((MediatorHandlerLifetime)42);
        Action typed = () => builder.AddRequestHandler<EchoQuery, string, EchoQueryHandler>((MediatorHandlerLifetime)42);

        untyped.Should().Throw<ArgumentOutOfRangeException>();
        typed.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LifetimeAttribute_ExposesLifetime()
    {
        new MediatorHandlerLifetimeAttribute(MediatorHandlerLifetime.Singleton).Lifetime.Should().Be(MediatorHandlerLifetime.Singleton);
    }

    private sealed record class PingCommand : IRequest;

    private sealed class PingCommandHandler : IRequestHandler<PingCommand>
    {
        public ValueTask<global::Atya.Foundation.Results.Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success());
    }

    private sealed class SecondPingCommandHandler : IRequestHandler<PingCommand>
    {
        public ValueTask<global::Atya.Foundation.Results.Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success());
    }

    private sealed record class EchoQuery(string Value) : IRequest<string>;

    private sealed class EchoQueryHandler : IRequestHandler<EchoQuery, string>
    {
        public ValueTask<global::Atya.Foundation.Results.Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success(request.Value));
    }

    private sealed class SecondEchoQueryHandler : IRequestHandler<EchoQuery, string>
    {
        public ValueTask<global::Atya.Foundation.Results.Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success(request.Value));
    }

    private sealed class TwoInterfaceHandler : IRequestHandler<PingCommand>, IRequestHandler<EchoQuery, string>
    {
        public ValueTask<global::Atya.Foundation.Results.Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success());

        public ValueTask<global::Atya.Foundation.Results.Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success(request.Value));
    }
}
