using System.Collections.Immutable;
using Atya.Application.Mediator.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Atya.Application.Mediator.UnitTests;

public sealed class MediatorRegistrationSourceGeneratorTests
{
    [Fact]
    public void Generator_DiscoveredHandlers_EmitsParameterlessRegistration()
    {
        const string source = """
            using Atya.Application.Mediator;
            using Atya.Foundation.Results;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Sample;

            public sealed record class EchoQuery(string Value) : IRequest<string>;

            public sealed class EchoHandler : IRequestHandler<EchoQuery, string>
            {
                public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success(request.Value));
            }
            """;

        GeneratorDriverRunResult result = RunGenerator(source);

        result.Diagnostics.Should().BeEmpty();
        result.Results.Should().ContainSingle();
        result.Results[0].GeneratedSources.Should().ContainSingle(sourceResult =>
            sourceResult.HintName == "AtyaMediatorGeneratedServiceCollectionExtensions.g.cs"
            && sourceResult.SourceText.ToString().Contains("AddAtyaMediator(this IServiceCollection services)", StringComparison.Ordinal)
            && sourceResult.SourceText.ToString().Contains(
                "builder.AddRequestHandler<global::Sample.EchoQuery, string, global::Sample.EchoHandler>();",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_DuplicateHandlers_ReportsDiagnostic()
    {
        const string source = """
            using Atya.Application.Mediator;
            using Atya.Foundation.Results;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Sample;

            public sealed record class EchoQuery(string Value) : IRequest<string>;

            public sealed class FirstHandler : IRequestHandler<EchoQuery, string>
            {
                public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success(request.Value));
            }

            public sealed class SecondHandler : IRequestHandler<EchoQuery, string>
            {
                public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success(request.Value));
            }
            """;

        GeneratorDriverRunResult result = RunGenerator(source, allowGeneratorErrors: true);

        result.Results[0].Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Id == "ATYAMEDIATOR001"
            && diagnostic.Severity == DiagnosticSeverity.Error);
        result.Results[0].GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Generator_UntypedHandler_EmitsUntypedRegistration()
    {
        const string source = """
            using Atya.Application.Mediator;
            using Atya.Foundation.Results;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Sample;

            public sealed record class PingCommand : IRequest;

            public sealed class PingHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        GeneratorDriverRunResult result = RunGenerator(source);

        result.Results[0].GeneratedSources.Should().ContainSingle(sourceResult =>
            sourceResult.SourceText.ToString().Contains(
                "builder.AddRequestHandler<global::Sample.PingCommand, global::Sample.PingHandler>();",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_AbstractAndNonHandlerClasses_EmitEmptyRegistration()
    {
        const string source = """
            using Atya.Application.Mediator;
            using Atya.Foundation.Results;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Sample;

            public sealed class PlainType
            {
            }

            public sealed record class PingCommand : IRequest;

            public abstract class AbstractPingHandler : IRequestHandler<PingCommand>
            {
                public abstract ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken);
            }
            """;

        GeneratorDriverRunResult result = RunGenerator(source);

        result.Results[0].GeneratedSources.Should().ContainSingle(sourceResult =>
            sourceResult.SourceText.ToString().Contains("AddAtyaMediator(this IServiceCollection services)", StringComparison.Ordinal)
            && !sourceResult.SourceText.ToString().Contains("AddRequestHandler<", StringComparison.Ordinal));
    }

    private static GeneratorDriverRunResult RunGenerator(string source, bool allowGeneratorErrors = false)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MediatorRegistrationSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

        if (!allowGeneratorErrors)
        {
            diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        outputCompilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        return driver.GetRunResult();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences() =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
}
