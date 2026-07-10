using Atya.Application.Mediator;
using Atya.Foundation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.Samples.ConsoleApp;

/// <summary>
/// Runs the sample console application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Sends a sample request through the mediator.
    /// </summary>
    public static async Task Main()
    {
        ServiceCollection services = new();
        services.AddAtyaMediator();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        Result<string> result = await mediator.Send<CreateGreeting, string>(new CreateGreeting("Atya"));

        Console.WriteLine(result.IsSuccess ? result.Value : result.Error.Message);
    }

    /// <summary>
    /// Sample request handled by the generated registration.
    /// </summary>
    public sealed record class CreateGreeting(string Name) : IRequest<string>;

    /// <summary>
    /// Sample request handler discovered by the source generator.
    /// </summary>
    public sealed class CreateGreetingHandler : IRequestHandler<CreateGreeting, string>
    {
        /// <inheritdoc />
        public ValueTask<Result<string>> Handle(CreateGreeting request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success($"Hello, {request.Name}."));
    }
}
