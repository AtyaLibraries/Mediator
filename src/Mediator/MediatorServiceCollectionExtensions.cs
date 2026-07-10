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
    /// <param name="configure">Handler-registration callback.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAtyaMediator(
        this IServiceCollection services,
        Action<MediatorRegistrationBuilder> configure)
    {
        Guard.AgainstNull(services);
        Guard.AgainstNull(configure);

        services.TryAddScoped<IMediator, Mediator>();
        configure(new MediatorRegistrationBuilder(services));

        return services;
    }
}
