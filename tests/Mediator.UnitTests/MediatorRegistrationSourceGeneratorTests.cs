using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Atya.Application.Mediator.SourceGeneration;
using Atya.Foundation.Results;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Atya.Application.Mediator.UnitTests;

public sealed class MediatorRegistrationSourceGeneratorTests
{
    private const string Header = """
        using Atya.Application.Mediator;
        using Atya.Foundation.Results;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Sample;

        """;

    [Fact]
    public void Generator_DiscoveredHandlers_EmitsParameterlessRegistration()
    {
        const string source = Header + """
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
                "builder.AddRequestHandler<global::Sample.EchoQuery, string, global::Sample.EchoHandler>(global::Atya.Application.Mediator.MediatorHandlerLifetime.Transient);",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_RecordHandler_IsDiscovered()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public sealed record class PingHandler(string Name) : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        GeneratedText(RunGenerator(source)).Should().Contain(
            "builder.AddRequestHandler<global::Sample.PingCommand, global::Sample.PingHandler>(global::Atya.Application.Mediator.MediatorHandlerLifetime.Transient);");
    }

    [Fact]
    public void Generator_RecordStructHandler_IsNotDiscovered()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public readonly record struct StructHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        GeneratedText(RunGenerator(source)).Should().NotContain("AddRequestHandler<");
    }

    [Fact]
    public void Generator_TypeWithTwoHandlerInterfaces_EmitsOneRegistrationPerInterface()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public sealed record class EchoQuery(string Value) : IRequest<string>;

            [MediatorHandlerLifetime(MediatorHandlerLifetime.Scoped)]
            public sealed class BothHandler : IRequestHandler<PingCommand>, IRequestHandler<EchoQuery, string>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());

                public ValueTask<Result<string>> Handle(EchoQuery request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success(request.Value));
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        generated.Should().Contain(
            "builder.AddRequestHandler<global::Sample.EchoQuery, string, global::Sample.BothHandler>(global::Atya.Application.Mediator.MediatorHandlerLifetime.Scoped);");
        generated.Should().Contain(
            "builder.AddRequestHandler<global::Sample.PingCommand, global::Sample.BothHandler>(global::Atya.Application.Mediator.MediatorHandlerLifetime.Scoped);");
    }

    [Theory]
    [InlineData("Singleton")]
    [InlineData("Transient")]
    public void Generator_LifetimeAttribute_IsEmitted(string lifetime)
    {
        string source = Header + $$"""
            public sealed record class PingCommand : IRequest;

            [MediatorHandlerLifetime(MediatorHandlerLifetime.{{lifetime}})]
            public sealed class PingHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        GeneratedText(RunGenerator(source)).Should().Contain($"MediatorHandlerLifetime.{lifetime});");
    }

    [Fact]
    public void Generator_DuplicateHandlers_ReportsDiagnostic()
    {
        const string source = Header + """
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
    public void Generator_OpenGenericHandler_ReportsDiagnostic()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public sealed class GenericHandler<T> : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        AssertError(source, "ATYAMEDIATOR002");
    }

    [Fact]
    public void Generator_PrivateNestedHandler_ReportsDiagnostic()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public static class Holder
            {
                private sealed class HiddenHandler : IRequestHandler<PingCommand>
                {
                    public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                        ValueTask.FromResult(Result.Success());
                }
            }
            """;

        AssertError(source, "ATYAMEDIATOR003");
    }

    [Fact]
    public void Generator_ConflictingLifetimeAttributes_ReportsDiagnostic()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            [MediatorHandlerLifetime(MediatorHandlerLifetime.Scoped)]
            [MediatorHandlerLifetime(MediatorHandlerLifetime.Singleton)]
            public sealed class PingHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        AssertError(source, "ATYAMEDIATOR004");
    }

    [Fact]
    public void Generator_PartialHandlerAcrossTwoDeclarations_IsRegisteredOnce()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public partial class PingHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }

            public partial class PingHandler : System.IDisposable
            {
                public void Dispose()
                {
                }
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        generated.Split(["AddRequestHandler<"], StringSplitOptions.None).Should().HaveCount(2);
    }

    [Fact]
    public void Generator_AbstractAndNonHandlerClasses_EmitEmptyRegistrationAndWarn()
    {
        const string source = Header + """
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
        result.Results[0].Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "ATYAMEDIATOR005" && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Generator_Output_IsDeterministicAcrossRunsAndInputOrder()
    {
        string[] sources =
        [
            Header + "public sealed record class A1 : IRequest; public sealed class HA1 : IRequestHandler<A1> { public ValueTask<Result> Handle(A1 r, CancellationToken c) => ValueTask.FromResult(Result.Success()); }",
            Header + "public sealed record class B1(string V) : IRequest<string>; public sealed class HB1 : IRequestHandler<B1, string> { public ValueTask<Result<string>> Handle(B1 r, CancellationToken c) => ValueTask.FromResult(Result.Success(r.V)); }",
            Header + "public sealed record class C1 : IRequest; [MediatorHandlerLifetime(MediatorHandlerLifetime.Scoped)] public sealed class HC1 : IRequestHandler<C1> { public ValueTask<Result> Handle(C1 r, CancellationToken c) => ValueTask.FromResult(Result.Success()); }",
        ];
        string[] namespaced = sources.Select((text, index) => text.Replace("namespace Sample;", $"namespace Sample{index};", StringComparison.Ordinal)).ToArray();

        string forward = GeneratedText(RunGenerator(namespaced));
        string again = GeneratedText(RunGenerator(namespaced));
        string reversed = GeneratedText(RunGenerator(namespaced.Reverse().ToArray()));
        string shuffled = GeneratedText(RunGenerator([namespaced[1], namespaced[2], namespaced[0]]));

        again.Should().Be(forward);
        reversed.Should().Be(forward);
        shuffled.Should().Be(forward);
        forward.Should().Contain("AddRequestHandler<");
    }

    [Fact]
    public void Generator_UntypedHandler_EmitsUntypedRegistration()
    {
        const string source = Header + """
            public sealed record class PingCommand : IRequest;

            public sealed class PingHandler : IRequestHandler<PingCommand>
            {
                public ValueTask<Result> Handle(PingCommand request, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(Result.Success());
            }
            """;

        GeneratedText(RunGenerator(source)).Should().Contain(
            "builder.AddRequestHandler<global::Sample.PingCommand, global::Sample.PingHandler>(global::Atya.Application.Mediator.MediatorHandlerLifetime.Transient);");
    }

    private static void AssertError(string source, string id)
    {
        GeneratorDriverRunResult result = RunGenerator(source, allowGeneratorErrors: true);

        result.Results[0].Diagnostics.Should().Contain(diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Error);
        result.Results[0].GeneratedSources.Should().BeEmpty();
    }

    private static string GeneratedText(GeneratorDriverRunResult result) =>
        result.Results[0].GeneratedSources.Single().SourceText.ToString();

    private static GeneratorDriverRunResult RunGenerator(string source, bool allowGeneratorErrors = false) =>
        RunGenerator([source], allowGeneratorErrors);

    private static GeneratorDriverRunResult RunGenerator(string[] sources, bool allowGeneratorErrors = false)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: sources.Select(static (text, index) => CSharpSyntaxTree.ParseText(text, path: $"File{index}.cs")),
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MediatorRegistrationSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

        if (!allowGeneratorErrors)
        {
            diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            outputCompilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        return driver.GetRunResult();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        Assembly[] requiredAssemblies =
        [
            typeof(IMediator).Assembly,
            typeof(Result).Assembly,
            typeof(IServiceCollection).Assembly,
        ];

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Concat(requiredAssemblies)
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .DistinctBy(static assembly => assembly.Location)
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
    }
}
