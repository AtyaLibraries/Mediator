# Atya.Application.Mediator

Source-generated, reflection-free mediator contracts and dispatch for .NET applications.

[![NuGet Version](https://img.shields.io/nuget/v/Atya.Application.Mediator?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&color=512BD4)](https://www.nuget.org/packages/Atya.Application.Mediator)
[![Downloads](https://img.shields.io/nuget/dt/Atya.Application.Mediator?style=for-the-badge&logo=nuget&logoColor=white&label=Downloads&color=512BD4)](https://www.nuget.org/packages/Atya.Application.Mediator)
![.NET 10.0](https://img.shields.io/badge/.NET_10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-512BD4?style=for-the-badge)](https://github.com/AtyaLibraries/Mediator/blob/development/LICENSE)

## Overview

`Atya.Application.Mediator` provides a small application-layer mediator for codebases that want explicit request and handler contracts without runtime assembly scanning. Handlers return `Result` or `Result<T>` so expected application outcomes stay in the Atya Results model instead of exceptions or framework-specific response objects.

The package is intentionally narrow: it owns request dispatch and registration shape. Validation, ProblemDetails mapping, endpoint filters, persistence, and pipeline frameworks stay in their owning packages or in the platform.

## Features

- Result-first request contracts for commands and queries.
- DI registration through `AddAtyaMediator`.
- Reflection-free handler registration through `MediatorRegistrationBuilder`.
- Source-generator-ready registration surface for generated code.
- Stable failure code when a request has no registered handler.

## Installation

```bash
dotnet add package Atya.Application.Mediator
```

```powershell
Install-Package Atya.Application.Mediator
```

```xml
<PackageReference Include="Atya.Application.Mediator" Version="<latest-stable>" />
```

## Quick Start

```csharp
using Atya.Application.Mediator;
using Atya.Foundation.Results;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
services.AddAtyaMediator(builder =>
    builder.AddRequestHandler<CreateGreeting, string, CreateGreetingHandler>());

using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();

Result<string> result = await mediator.Send<CreateGreeting, string>(new CreateGreeting("Atya"));

Console.WriteLine(result.IsSuccess ? result.Value : result.Error.Message);

public sealed record class CreateGreeting(string Name) : IRequest<string>;

public sealed class CreateGreetingHandler : IRequestHandler<CreateGreeting, string>
{
    public ValueTask<Result<string>> Handle(CreateGreeting request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success($"Hello, {request.Name}."));
}
```

## Contracts

Use `IRequest` for commands that return only success or failure:

```csharp
public sealed record class ArchiveOrder(Guid OrderId) : IRequest;

public sealed class ArchiveOrderHandler : IRequestHandler<ArchiveOrder>
{
    public ValueTask<Result> Handle(ArchiveOrder request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }
}
```

Use `IRequest<TResponse>` for queries or commands with a value:

```csharp
public sealed record class GetOrder(Guid OrderId) : IRequest<OrderSummary>;

public sealed class GetOrderHandler : IRequestHandler<GetOrder, OrderSummary>
{
    public ValueTask<Result<OrderSummary>> Handle(GetOrder request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success(new OrderSummary(request.OrderId)));
    }
}

public sealed record class OrderSummary(Guid OrderId);
```

## Registration

Register the runtime once and add request handlers through the builder:

```csharp
services.AddAtyaMediator(builder =>
{
    builder.AddRequestHandler<ArchiveOrder, ArchiveOrderHandler>();
    builder.AddRequestHandler<GetOrder, OrderSummary, GetOrderHandler>();
});
```

The builder is the source-generator contract. Generated registration code is expected to emit calls to the same `AddRequestHandler` methods after discovering concrete `IRequestHandler<...>` implementations at compile time. The runtime does not scan assemblies or use reflection to discover handlers.

Generated code should be deterministic, register each request once, and fail compilation when multiple handlers target the same request and response shape. Infrastructure dispatcher interfaces are public only so generated code and package consumers can compose registrations without reflection; they are marked hidden from editor browsing.

## Error Codes

| Code | Kind | Meaning |
| --- | --- | --- |
| `atya.application.mediator.handler_not_registered` | `NotFound` | No handler dispatcher is registered for the request type sent to `IMediator`. |

## Why These Dependencies

- `Atya.Foundation.Guards` validates public entry points and configuration arguments.
- `Atya.Foundation.Results` is the package's expected-outcome model.
- `Microsoft.Extensions.DependencyInjection.Abstractions` provides the DI registration surface without forcing a concrete container.

## Compatibility

Targets `net10.0`.

## Testing

```bash
dotnet test
```

## Benchmarks

Benchmarks live in `benchmarks/Mediator.Benchmarks`.

```bash
dotnet run --project benchmarks/Mediator.Benchmarks/Mediator.Benchmarks.csproj -c Release -- --list flat
```

## License

Released under the MIT license. See [LICENSE](https://github.com/AtyaLibraries/Mediator/blob/development/LICENSE).

## Links

- [NuGet package](https://www.nuget.org/packages/Atya.Application.Mediator)
- [Source repository](https://github.com/AtyaLibraries/Mediator)
- [Atya Libraries](https://github.com/AtyaLibraries)
