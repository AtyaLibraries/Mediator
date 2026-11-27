using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Atya.Application.Mediator.UnitTests;

/// <summary>
/// Packs the library and builds a fresh consumer that references only the resulting .nupkg (no project references),
/// proving that the packed analyzer path generates the registration and that dispatch works.
/// </summary>
public sealed class PackedConsumerTests
{
    private const string AnalyzerEntry = "analyzers/dotnet/cs/Atya.Application.Mediator.SourceGeneration.dll";

    [Fact]
    [Trait("Category", "PackedConsumer")]
    public async Task PackedPackage_GeneratesRegistrationInAFreshConsumer()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string repositoryRoot = FindRepositoryRoot();
        string work = Path.Combine(Path.GetTempPath(), "atya-mediator-pack-" + Guid.NewGuid().ToString("N"));
        string feed = Path.Combine(work, "feed");
        string consumer = Path.Combine(work, "consumer");
        Directory.CreateDirectory(feed);
        Directory.CreateDirectory(consumer);

        try
        {
            await RunDotnetAsync(
                repositoryRoot,
                cancellation,
                "pack",
                Path.Combine(repositoryRoot, "src", "Mediator", "Mediator.csproj"),
                "--configuration",
                "Release",
                "--output",
                feed);

            string package = Directory.GetFiles(feed, "Atya.Application.Mediator.*.nupkg").Single();
            string version = Path.GetFileNameWithoutExtension(package)["Atya.Application.Mediator.".Length..];

            using (ZipArchive archive = ZipFile.OpenRead(package))
            {
                archive.Entries.Select(entry => entry.FullName).Should().Contain(AnalyzerEntry);
            }

            await File.WriteAllTextAsync(Path.Combine(consumer, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{feed}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """, cancellation);
            await File.WriteAllTextAsync(Path.Combine(consumer, "Consumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>generated</CompilerGeneratedFilesOutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Atya.Application.Mediator" Version="{version}" />
                    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.15" />
                  </ItemGroup>
                </Project>
                """, cancellation);
            await File.WriteAllTextAsync(Path.Combine(consumer, "Program.cs"), """
                using Atya.Application.Mediator;
                using Atya.Foundation.Results;
                using Microsoft.Extensions.DependencyInjection;

                ServiceCollection services = new();
                services.AddAtyaMediator();
                using ServiceProvider provider = services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
                using IServiceScope scope = provider.CreateScope();

                Result<string> result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new Greet("packed"));
                Console.WriteLine(result.Value);

                public sealed record Greet(string Name) : IRequest<string>;

                [MediatorHandlerLifetime(MediatorHandlerLifetime.Scoped)]
                public sealed record GreetHandler : IRequestHandler<Greet, string>
                {
                    public ValueTask<Result<string>> Handle(Greet request, CancellationToken cancellationToken) =>
                        ValueTask.FromResult(Result.Success("hello " + request.Name));
                }
                """, cancellation);

            string output = await RunDotnetAsync(consumer, cancellation, "run", "--project", Path.Combine(consumer, "Consumer.csproj"));

            output.Should().Contain("hello packed");
            Directory.GetFiles(Path.Combine(consumer, "generated"), "AtyaMediatorGeneratedServiceCollectionExtensions.g.cs", SearchOption.AllDirectories)
                .Should().NotBeEmpty("the packed analyzer must have generated the registration");
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temporary directory.
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mediator.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Mediator.sln was not found above the test output directory.");
    }

    private static async Task<string> RunDotnetAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        using Process process = Process.Start(startInfo)!;
        StringBuilder log = new();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string standardOutput = await stdout;
        log.AppendLine(standardOutput).AppendLine(await stderr);

        process.ExitCode.Should().Be(0, "dotnet {0} failed:{1}{2}", string.Join(' ', arguments), Environment.NewLine, log.ToString());

        return standardOutput;
    }
}
