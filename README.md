# Atya.Application.Mediator

Source-generated, reflection-free mediator contracts and dispatch for .NET applications.

[![NuGet Version](https://img.shields.io/nuget/v/Atya.Application.Mediator?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&color=512BD4)](https://www.nuget.org/packages/Atya.Application.Mediator)
[![Downloads](https://img.shields.io/nuget/dt/Atya.Application.Mediator?style=for-the-badge&logo=nuget&logoColor=white&label=Downloads&color=512BD4)](https://www.nuget.org/packages/Atya.Application.Mediator)
![.NET 10.0](https://img.shields.io/badge/.NET_10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-512BD4?style=for-the-badge)](LICENSE)

## Overview

`Atya.Application.Mediator` provides a small application-layer mediator for codebases that want explicit request and handler contracts without runtime assembly scanning. Handlers return `Result` or `Result<T>` so expected application outcomes stay in the Atya Results model instead of exceptions or framework-specific response objects.

The package is intentionally narrow: it owns request dispatch, compile-time handler registration, and DI wiring. Validation, ProblemDetails mapping, endpoint filters, persistence, notifications, and pipeline behavior frameworks stay outside v1.

## Features

- Result-first request contracts for commands and queries.
- Inference-friendly `await mediator.Send(query)` call sites.
- Source-generated `services.AddAtyaMediator()` registration for compile-time-discovered handlers.
- Reflection-free runtime dispatch through frozen runtime-type lookup tables.
- Duplicate handler diagnostics at compile time.
- Manual `MediatorRegistrationBuilder` registration as an escape hatch.

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
services.AddAtyaMediator();

using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();

Result<string> result = await mediator.Send(new CreateGreeting("Atya"));

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

## Sending Requests

Use inference-friendly overloads for normal call sites:

```csharp
Result archive = await mediator.Send(new ArchiveOrder(orderId));
Result<OrderSummary> order = await mediator.Send(new GetOrder(orderId));
```

The two-generic overloads remain available as explicit fast paths:

```csharp
Result archive = await mediator.Send<ArchiveOrder>(new ArchiveOrder(orderId));
Result<OrderSummary> order = await mediator.Send<GetOrder, OrderSummary>(new GetOrder(orderId));
```

Missing handlers are configuration errors. `IMediator.Send` throws `InvalidOperationException` naming the request type and the registration fix when no handler is registered. Missing and duplicate registrations are treated consistently as configuration failures, not Result failures.

## Registration

In normal applications, call the generated one-line registration:

```csharp
services.AddAtyaMediator();
```

The package includes a Roslyn source generator packaged as an analyzer asset. It discovers concrete `IRequestHandler<TRequest>` and `IRequestHandler<TRequest,TResponse>` implementations at compile time and emits deterministic registration code. If more than one handler targets the same request/response shape, the generator reports `ATYAMEDIATOR001` as a compile-time error.

Manual registration remains available for tests and advanced composition:

```csharp
services.AddAtyaMediator(builder =>
{
    builder.AddRequestHandler<ArchiveOrder, ArchiveOrderHandler>();
    builder.AddRequestHandler<GetOrder, OrderSummary, GetOrderHandler>();
});
```

Manual registration uses the same runtime dispatcher bridge as generated registration. The runtime does not scan assemblies.

## Deliberate Omissions

v1 deliberately does not include pipeline behaviors, notifications, streaming requests, or handler lifetime policies beyond DI registration. Pipeline behaviors are planned as an additive `1.1.0` feature after the v1 dispatch and source-generation surface is stable.

## Why These Dependencies

- `Atya.Foundation.Guards` validates public entry points and configuration arguments.
- `Atya.Foundation.Results` is the package's expected-outcome model.
- `Microsoft.Extensions.DependencyInjection.Abstractions` provides the DI registration surface without forcing a concrete container.

The Roslyn source generator is packaged as an analyzer asset and does not add runtime dependencies to consuming applications.

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

Released under the MIT license. See [LICENSE](LICENSE).

## Links

- [NuGet package](https://www.nuget.org/packages/Atya.Application.Mediator)
- [Source repository](https://github.com/AtyaLibraries/Mediator)
- [Atya Libraries](https://github.com/AtyaLibraries)
