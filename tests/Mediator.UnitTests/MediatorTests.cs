using Atya.Foundation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.UnitTests;

public sealed class MediatorTests
{
    [Fact]
    public async Task Send_UntypedRegisteredHandler_ReturnsHandlerResult()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<PingCommand, PingCommandHandler>());

        Result result = await mediator.Send(new PingCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Send_TypedRegisteredHandler_ReturnsHandlerResult()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<EchoQuery, string, EchoQueryHandler>());

        Result<string> result = await mediator.Send<EchoQuery, string>(
            new EchoQuery("atya"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("atya");
    }

    [Fact]
    public async Task Send_HandlerReturnsFailure_PropagatesFailure()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<RejectCommand, RejectCommandHandler>());

        Result result = await mediator.Send(new RejectCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("sample.rejected");
    }

    [Fact]
    public async Task Send_InferenceUntypedRegisteredHandler_ReturnsHandlerResult()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<PingCommand, PingCommandHandler>());

        Result result = await mediator.Send((IRequest)new PingCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Send_InferenceTypedRegisteredHandler_ReturnsHandlerResult()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<EchoQuery, string, EchoQueryHandler>());

        Result<string> result = await mediator.Send((IRequest<string>)new EchoQuery("atya"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("atya");
    }

    [Fact]
    public async Task Send_UntypedRequestWithoutHandler_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send(new PingCommand(), TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*PingCommand*Register the handler*");
    }

    [Fact]
    public async Task Send_TypedRequestWithoutHandler_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send<EchoQuery, string>(
            new EchoQuery("atya"),
            TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*EchoQuery*Register the handler*");
    }

    [Fact]
    public async Task Send_InferenceUntypedRequestWithoutHandler_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send((IRequest)new PingCommand(), TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*PingCommand*Register the handler*");
    }

    [Fact]
    public async Task Send_InferenceTypedRequestWithoutHandler_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send((IRequest<string>)new EchoQuery("atya"), TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*EchoQuery*Register the handler*");
    }

    [Fact]
    public async Task Send_NullUntypedRequest_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send((PingCommand)null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Send_NullTypedRequest_Throws()
    {
        IMediator mediator = CreateMediator();

        Func<Task> act = async () => await mediator.Send<EchoQuery, string>(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Send_CanceledToken_Throws()
    {
        IMediator mediator = CreateMediator(builder =>
            builder.AddRequestHandler<PingCommand, PingCommandHandler>());
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        Func<Task> act = async () => await mediator.Send(new PingCommand(), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static IMediator CreateMediator(Action<MediatorRegistrationBuilder>? configure = null)
    {
        ServiceCollection services = new();
        services.AddAtyaMediator(configure ?? (_ => { }));

        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    private sealed record class PingCommand : IRequest;

    private sealed class PingCommandHandler : IRequestHandler<PingCommand>
    {
        public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());
    }

    private sealed record class RejectCommand : IRequest;

    private sealed class RejectCommandHandler : IRequestHandler<RejectCommand>
    {
        public ValueTask<Result> Handle(RejectCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure("sample.rejected", "The sample command was rejected."));
    }

    private sealed record class EchoQuery(string Value) : IRequest<string>;

    private sealed class EchoQueryHandler : IRequestHandler<EchoQuery, string>
    {
        public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success(request.Value));
    }
}
