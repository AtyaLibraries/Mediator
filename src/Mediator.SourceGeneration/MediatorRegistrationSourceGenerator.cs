using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Atya.Application.Mediator.SourceGeneration;

/// <summary>
/// Generates the parameterless AddAtyaMediator registration extension for compile-time-discovered handlers.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MediatorRegistrationSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateHandlerDescriptor = new(
        id: "ATYAMEDIATOR001",
        title: "Duplicate mediator handlers",
        messageFormat: "Multiple mediator handlers target request '{0}'. Keep exactly one handler for each request/response shape.",
        category: "Atya.Application.Mediator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Atya.Application.Mediator source generation requires one handler for each request/response shape.");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<HandlerRegistration?> handlers =
            context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (syntaxContext, cancellationToken) => GetHandlerRegistration(syntaxContext, cancellationToken))
            .Where(static handler => handler is not null);

        context.RegisterSourceOutput(
            handlers.Collect(),
            static (productionContext, collectedHandlers) => Emit(productionContext, collectedHandlers));
    }

    private static HandlerRegistration? GetHandlerRegistration(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol? classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;
        if (classSymbol is null || classSymbol.IsAbstract)
        {
            return null;
        }

        foreach (INamedTypeSymbol interfaceSymbol in classSymbol.AllInterfaces)
        {
            if (!IsMediatorHandlerInterface(interfaceSymbol))
            {
                continue;
            }

            string handlerType = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string requestType = interfaceSymbol.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string? responseType = interfaceSymbol.TypeArguments.Length == 2
                ? interfaceSymbol.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;

            Location? location = classDeclaration.Identifier.GetLocation();

            return new HandlerRegistration(handlerType, requestType, responseType, location);
        }

        return null;
    }

    private static bool IsMediatorHandlerInterface(INamedTypeSymbol interfaceSymbol)
    {
        INamedTypeSymbol originalDefinition = interfaceSymbol.OriginalDefinition;

        return originalDefinition.Name == "IRequestHandler"
            && originalDefinition.ContainingNamespace.ToDisplayString() == "Atya.Application.Mediator"
            && (originalDefinition.Arity == 1 || originalDefinition.Arity == 2);
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<HandlerRegistration?> collectedHandlers)
    {
        ImmutableArray<HandlerRegistration> handlers = collectedHandlers
            .Where(static handler => handler is not null)
            .Select(static handler => handler!.Value)
            .OrderBy(static handler => handler.RequestType, StringComparer.Ordinal)
            .ThenBy(static handler => handler.ResponseType ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static handler => handler.HandlerType, StringComparer.Ordinal)
            .ToImmutableArray();

        bool hasDuplicate = ReportDuplicates(context, handlers);
        if (hasDuplicate)
        {
            return;
        }

        context.AddSource("AtyaMediatorGeneratedServiceCollectionExtensions.g.cs", SourceText.From(GenerateSource(handlers), Encoding.UTF8));
    }

    private static bool ReportDuplicates(
        SourceProductionContext context,
        ImmutableArray<HandlerRegistration> handlers)
    {
        bool hasDuplicate = false;
        foreach (IGrouping<string, HandlerRegistration> group in handlers.GroupBy(static handler => handler.Key, StringComparer.Ordinal))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            hasDuplicate = true;
            foreach (HandlerRegistration handler in group)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateHandlerDescriptor,
                    handler.Location,
                    handler.ResponseType is null
                        ? handler.RequestType
                        : handler.RequestType + " -> " + handler.ResponseType));
            }
        }

        return hasDuplicate;
    }

    private static string GenerateSource(ImmutableArray<HandlerRegistration> handlers)
    {
        StringBuilder builder = new();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine("namespace Atya.Application.Mediator;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Provides source-generated mediator service registration.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("public static class AtyaMediatorGeneratedServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Adds the mediator runtime and all compile-time-discovered request handlers.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to update.</param>");
        builder.AppendLine("    /// <returns>The updated service collection.</returns>");
        builder.AppendLine("    public static IServiceCollection AddAtyaMediator(this IServiceCollection services)");
        builder.AppendLine("    {");
        builder.AppendLine("        return MediatorServiceCollectionExtensions.AddAtyaMediator(services, builder =>");
        builder.AppendLine("        {");

        foreach (HandlerRegistration handler in handlers)
        {
            if (handler.ResponseType is null)
            {
                builder.Append("            builder.AddRequestHandler<");
                builder.Append(handler.RequestType);
                builder.Append(", ");
                builder.Append(handler.HandlerType);
                builder.AppendLine(">();");
            }
            else
            {
                builder.Append("            builder.AddRequestHandler<");
                builder.Append(handler.RequestType);
                builder.Append(", ");
                builder.Append(handler.ResponseType);
                builder.Append(", ");
                builder.Append(handler.HandlerType);
                builder.AppendLine(">();");
            }
        }

        builder.AppendLine("        });");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private readonly struct HandlerRegistration
    {
        public HandlerRegistration(
            string handlerType,
            string requestType,
            string? responseType,
            Location? location)
        {
            HandlerType = handlerType;
            RequestType = requestType;
            ResponseType = responseType;
            Location = location;
        }

        public string HandlerType { get; }

        public string RequestType { get; }

        public string? ResponseType { get; }

        public Location? Location { get; }

        public string Key => ResponseType is null ? RequestType : RequestType + " -> " + ResponseType;
    }
}
