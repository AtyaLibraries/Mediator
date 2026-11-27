using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Atya.Application.Mediator.SourceGeneration;

/// <summary>
/// Generates the parameterless AddAtyaMediator registration extension for compile-time-discovered handlers.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MediatorRegistrationSourceGenerator : IIncrementalGenerator
{
    private const string Category = "Atya.Application.Mediator";

    private static readonly DiagnosticDescriptor DuplicateHandlerDescriptor = new(
        id: "ATYAMEDIATOR001",
        title: "Duplicate mediator handlers",
        messageFormat: "Multiple mediator handlers target request '{0}'. Keep exactly one handler for each request/response shape.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Atya.Application.Mediator source generation requires one handler for each request/response shape.");

    private static readonly DiagnosticDescriptor OpenGenericHandlerDescriptor = new(
        id: "ATYAMEDIATOR002",
        title: "Open generic mediator handler",
        messageFormat: "Handler '{0}' is an open generic type. Close the type or register it manually with MediatorRegistrationBuilder.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleHandlerDescriptor = new(
        id: "ATYAMEDIATOR003",
        title: "Inaccessible mediator handler",
        messageFormat: "Handler '{0}' is not accessible from the generated registration code. Make it public or internal.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConflictingLifetimeDescriptor = new(
        id: "ATYAMEDIATOR004",
        title: "Conflicting mediator handler lifetimes",
        messageFormat: "Handler '{0}' declares conflicting MediatorHandlerLifetime attributes. A handler has exactly one lifetime.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AbstractHandlerDescriptor = new(
        id: "ATYAMEDIATOR005",
        title: "Abstract mediator handler skipped",
        messageFormat: "Type '{0}' is abstract or static and implements a mediator handler interface; it is not registered. Concrete derived types are registered instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<HandlerCandidate?> handlers =
            context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateNode(node),
                transform: static (syntaxContext, cancellationToken) => GetCandidate(syntaxContext, cancellationToken))
            .Where(static handler => handler is not null);

        context.RegisterSourceOutput(
            handlers.Collect(),
            static (productionContext, collectedHandlers) => Emit(productionContext, collectedHandlers));
    }

    private static bool IsCandidateNode(SyntaxNode node) =>
        node is TypeDeclarationSyntax { BaseList: not null } type
        && (type.IsKind(SyntaxKind.ClassDeclaration) || type.IsKind(SyntaxKind.RecordDeclaration));

    private static HandlerCandidate? GetCandidate(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol
            || symbol.TypeKind != TypeKind.Class)
        {
            return null;
        }

        List<InterfaceRegistration> interfaces = new();
        foreach (INamedTypeSymbol interfaceSymbol in symbol.AllInterfaces)
        {
            if (!IsMediatorHandlerInterface(interfaceSymbol))
            {
                continue;
            }

            interfaces.Add(new InterfaceRegistration(
                interfaceSymbol.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                interfaceSymbol.TypeArguments.Length == 2
                    ? interfaceSymbol.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : null));
        }

        if (interfaces.Count == 0)
        {
            return null;
        }

        string handlerType = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Location location = declaration.Identifier.GetLocation();

        if (symbol.IsAbstract || symbol.IsStatic)
        {
            return new HandlerCandidate(handlerType, location, CandidateKind.Abstract, "Transient", interfaces);
        }

        if (IsOpenGeneric(symbol))
        {
            return new HandlerCandidate(handlerType, location, CandidateKind.OpenGeneric, "Transient", interfaces);
        }

        if (!IsAccessible(symbol))
        {
            return new HandlerCandidate(handlerType, location, CandidateKind.Inaccessible, "Transient", interfaces);
        }

        HashSet<string> lifetimes = new(StringComparer.Ordinal);
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "MediatorHandlerLifetimeAttribute" } attributeClass
                && attributeClass.ContainingNamespace.ToDisplayString() == "Atya.Application.Mediator"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is int value)
            {
                lifetimes.Add(LifetimeName(value));
            }
        }

        if (lifetimes.Count > 1)
        {
            return new HandlerCandidate(handlerType, location, CandidateKind.ConflictingLifetime, "Transient", interfaces);
        }

        string lifetime = lifetimes.Count == 1 ? lifetimes.First() : "Transient";
        return new HandlerCandidate(handlerType, location, CandidateKind.Ok, lifetime, interfaces);
    }

    private static string LifetimeName(int value) => value switch
    {
        1 => "Scoped",
        2 => "Singleton",
        _ => "Transient",
    };

    private static bool IsOpenGeneric(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAccessible(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
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
        ImmutableArray<HandlerCandidate?> collectedHandlers)
    {
        // A partial type is visited once per declaration; keep one candidate per handler type.
        List<HandlerCandidate> candidates = collectedHandlers
            .Where(static handler => handler is not null)
            .Select(static handler => handler!)
            .GroupBy(static handler => handler.HandlerType, StringComparer.Ordinal)
            .Select(static group => group.OrderBy(static handler => handler.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static handler => handler.Location.SourceSpan.Start)
                .First())
            .OrderBy(static handler => handler.HandlerType, StringComparer.Ordinal)
            .ToList();

        bool hasError = false;
        foreach (HandlerCandidate candidate in candidates)
        {
            switch (candidate.Kind)
            {
                case CandidateKind.Abstract:
                    context.ReportDiagnostic(Diagnostic.Create(AbstractHandlerDescriptor, candidate.Location, candidate.HandlerType));
                    break;
                case CandidateKind.OpenGeneric:
                    hasError = true;
                    context.ReportDiagnostic(Diagnostic.Create(OpenGenericHandlerDescriptor, candidate.Location, candidate.HandlerType));
                    break;
                case CandidateKind.Inaccessible:
                    hasError = true;
                    context.ReportDiagnostic(Diagnostic.Create(InaccessibleHandlerDescriptor, candidate.Location, candidate.HandlerType));
                    break;
                case CandidateKind.ConflictingLifetime:
                    hasError = true;
                    context.ReportDiagnostic(Diagnostic.Create(ConflictingLifetimeDescriptor, candidate.Location, candidate.HandlerType));
                    break;
            }
        }

        List<HandlerRegistration> registrations = candidates
            .Where(static candidate => candidate.Kind == CandidateKind.Ok)
            .SelectMany(static candidate => candidate.Interfaces.Select(
                shape => new HandlerRegistration(candidate.HandlerType, shape.RequestType, shape.ResponseType, candidate.Location, candidate.Lifetime)))
            .OrderBy(static handler => handler.RequestType, StringComparer.Ordinal)
            .ThenBy(static handler => handler.ResponseType ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static handler => handler.HandlerType, StringComparer.Ordinal)
            .ToList();

        if (ReportDuplicates(context, registrations) || hasError)
        {
            return;
        }

        context.AddSource("AtyaMediatorGeneratedServiceCollectionExtensions.g.cs", SourceText.From(GenerateSource(registrations), Encoding.UTF8));
    }

    private static bool ReportDuplicates(
        SourceProductionContext context,
        List<HandlerRegistration> handlers)
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

    private static string GenerateSource(List<HandlerRegistration> handlers)
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
            builder.Append("            builder.AddRequestHandler<");
            builder.Append(handler.RequestType);
            builder.Append(", ");
            if (handler.ResponseType is not null)
            {
                builder.Append(handler.ResponseType);
                builder.Append(", ");
            }

            builder.Append(handler.HandlerType);
            builder.Append(">(global::Atya.Application.Mediator.MediatorHandlerLifetime.");
            builder.Append(handler.Lifetime);
            builder.AppendLine(");");
        }

        builder.AppendLine("        });");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private enum CandidateKind
    {
        Ok,
        Abstract,
        OpenGeneric,
        Inaccessible,
        ConflictingLifetime,
    }

    private sealed class InterfaceRegistration
    {
        public InterfaceRegistration(string requestType, string? responseType)
        {
            RequestType = requestType;
            ResponseType = responseType;
        }

        public string RequestType { get; }

        public string? ResponseType { get; }
    }

    private sealed class HandlerCandidate
    {
        public HandlerCandidate(
            string handlerType,
            Location location,
            CandidateKind kind,
            string lifetime,
            List<InterfaceRegistration> interfaces)
        {
            HandlerType = handlerType;
            Location = location;
            Kind = kind;
            Lifetime = lifetime;
            Interfaces = interfaces;
        }

        public string HandlerType { get; }

        public Location Location { get; }

        public CandidateKind Kind { get; }

        public string Lifetime { get; }

        public List<InterfaceRegistration> Interfaces { get; }
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(
            string handlerType,
            string requestType,
            string? responseType,
            Location location,
            string lifetime)
        {
            HandlerType = handlerType;
            RequestType = requestType;
            ResponseType = responseType;
            Location = location;
            Lifetime = lifetime;
        }

        public string HandlerType { get; }

        public string RequestType { get; }

        public string? ResponseType { get; }

        public Location Location { get; }

        public string Lifetime { get; }

        public string Key => ResponseType is null ? RequestType : RequestType + " -> " + ResponseType;
    }
}
