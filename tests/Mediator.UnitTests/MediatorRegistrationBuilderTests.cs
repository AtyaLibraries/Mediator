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

        Action act = () => services.AddAtyaMediator();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
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
    public void AddAtyaMediator_DuplicateUntypedDispatcher_ThrowsWhenMediatorIsResolved()
    {
        ServiceCollection services = new();
        services.AddAtyaMediator();
        services.AddSingleton<IMediatorRequestDispatcher>(new DuplicatePingDispatcher());
        services.AddSingleton<IMediatorRequestDispatcher>(new DuplicatePingDispatcher());
        using ServiceProvider provider = services.BuildServiceProvider();

        Action act = () => _ = provider.GetRequiredService<IMediator>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PingCommand*");
    }

    [Fact]
    public void AddAtyaMediator_DuplicateTypedDispatcher_ThrowsWhenMediatorIsResolved()
    {
        ServiceCollection services = new();
        services.AddAtyaMediator();
        services.AddSingleton<IMediatorResponseDispatcher>(new DuplicateEchoDispatcher());
        services.AddSingleton<IMediatorResponseDispatcher>(new DuplicateEchoDispatcher());
        using ServiceProvider provider = services.BuildServiceProvider();

        Action act = () => _ = provider.GetRequiredService<IMediator>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*EchoQuery*");
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

    private sealed class DuplicatePingDispatcher : IMediatorRequestDispatcher
    {
        public Type RequestType => typeof(PingCommand);

        public ValueTask<global::Atya.Foundation.Results.Result> Dispatch(object request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(global::Atya.Foundation.Results.Result.Success());
    }

    private sealed class DuplicateEchoDispatcher : IMediatorResponseDispatcher
    {
        public Type RequestType => typeof(EchoQuery);

        public Type ResponseType => typeof(string);

        public ValueTask<object> Dispatch(object request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<object>(global::Atya.Foundation.Results.Result.Success(string.Empty));
    }
}
