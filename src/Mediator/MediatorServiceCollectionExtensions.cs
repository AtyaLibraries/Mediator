using Atya.Foundation.Guards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atya.Application.Mediator;

/// <summary>
/// Provides service-registration helpers for the mediator runtime.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the mediator runtime and request handlers to the service collection.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Optional handler-registration callback.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAtyaMediator(
        this IServiceCollection services,
        Action<MediatorRegistrationBuilder>? configure = null)
    {
        Guard.AgainstNull(services);

        services.TryAddScoped<IMediator, Mediator>();
        configure?.Invoke(new MediatorRegistrationBuilder(services));

        return services;
    }
}
