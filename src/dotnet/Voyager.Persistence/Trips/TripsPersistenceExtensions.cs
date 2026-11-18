using Microsoft.Extensions.DependencyInjection;

using Voyager.Core.Trips;

namespace Voyager.Persistence.Trips;

/// <summary>
/// Registers the repositories of the trips and the travelers.
/// </summary>
public static class TripsPersistenceExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers the repositories of the trips and the travelers, one per request like the database they use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddTripsRepositories(this IServiceCollection services)
    {
        services.AddScoped<ITravelerRepository, TravelerRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        return services;
    }

    #endregion Public Methods
}
