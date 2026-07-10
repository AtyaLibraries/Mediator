using Atya.Foundation.Results;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.Benchmarks;

/// <summary>
/// Benchmarks mediator dispatch hot paths.
/// </summary>
[MemoryDiagnoser]
public class MediatorDispatchBenchmarks
{
    private DirectEchoHandler _handler = null!;
    private IMediator _mediator = null!;
    private EchoQuery _request = null!;

    /// <summary>
    /// Creates reusable benchmark fixtures.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _request = new EchoQuery("atya");
        _handler = new DirectEchoHandler();

        ServiceCollection services = new();
        services.AddAtyaMediator();

        _mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    /// <summary>
    /// Calls the handler directly.
    /// </summary>
    /// <returns>The direct handler result.</returns>
    [Benchmark(Baseline = true)]
    public ValueTask<Result<string>> DirectHandler() =>
        _handler.Handle(_request, CancellationToken.None);

    /// <summary>
    /// Dispatches through the mediator.
    /// </summary>
    /// <returns>The mediator result.</returns>
    [Benchmark]
    public ValueTask<Result<string>> MediatorSendGenericFastPath() =>
        _mediator.Send<EchoQuery, string>(_request, CancellationToken.None);

    /// <summary>
    /// Dispatches through the inference-friendly runtime-type lookup path.
    /// </summary>
    /// <returns>The mediator result.</returns>
    [Benchmark]
    public ValueTask<Result<string>> MediatorSendInferencePath() =>
        _mediator.Send((IRequest<string>)_request, CancellationToken.None);

    /// <summary>
    /// Benchmark request discovered by the source generator.
    /// </summary>
    public sealed record class EchoQuery(string Value) : IRequest<string>;

    /// <summary>
    /// Benchmark handler discovered by the source generator.
    /// </summary>
    public sealed class DirectEchoHandler : IRequestHandler<EchoQuery, string>
    {
        /// <inheritdoc />
        public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success(request.Value));
    }
}
