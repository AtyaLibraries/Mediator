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
        services.AddAtyaMediator(builder =>
            builder.AddRequestHandler<CreateGreeting, string, CreateGreetingHandler>());

        using ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        Result<string> result = await mediator.Send<CreateGreeting, string>(new CreateGreeting("Atya"));

        Console.WriteLine(result.IsSuccess ? result.Value : result.Error.Message);
    }

    private sealed record class CreateGreeting(string Name) : IRequest<string>;

    private sealed class CreateGreetingHandler : IRequestHandler<CreateGreeting, string>
    {
        public ValueTask<Result<string>> Handle(CreateGreeting request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success($"Hello, {request.Name}."));
    }
}
