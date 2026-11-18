using AwesomeAssertions;

using Voyager.Core.Trips;

namespace Voyager.Persistence.Tests.Support;

/// <summary>Puts made-up data in a test database, and runs operations at the same time.</summary>
internal static class TestDatabaseSeeding
{
    /// <summary>Keeps a traveler, which must work.</summary>
    /// <param name="database">The database.</param>
    /// <param name="traveler">The traveler.</param>
    /// <returns>The same traveler.</returns>
    public static async Task<Traveler> SeedAsync(this TestDatabase database, Traveler traveler)
    {
        (await database.Travelers.TryAddAsync(traveler, CancellationToken.None)).Should().BeTrue("the traveler is made to be kept");
        return traveler;
    }

    /// <summary>Keeps a trip, which must work.</summary>
    /// <param name="database">The database.</param>
    /// <param name="trip">The trip.</param>
    /// <returns>The same trip.</returns>
    public static async Task<Trip> SeedAsync(this TestDatabase database, Trip trip)
    {
        (await database.Trips.TryAddAsync(trip, CancellationToken.None)).Should().BeTrue("the trip is made to be kept");
        return trip;
    }

    /// <summary>Reads the travelers of a trip, in order.</summary>
    /// <param name="database">The database.</param>
    /// <param name="trip">The trip, which must exist.</param>
    /// <returns>Its travelers.</returns>
    public static async Task<IReadOnlyList<TravelerId>> TravelersOfAsync(this TestDatabase database, TripId trip)
    {
        var found = await database.Trips.FindAsync(trip, CancellationToken.None);
        found.Should().NotBeNull("the trip is expected to exist");
        return found!.Travelers;
    }

    /// <summary>
    /// Starts operations together, each on its own thread, once all of them are ready: two members acting at the same time.
    /// </summary>
    /// <typeparam name="T">What each operation gives.</typeparam>
    /// <param name="operations">The operations.</param>
    /// <returns>Their results, in the order of the operations.</returns>
    public static async Task<T[]> RunTogetherAsync<T>(params Func<Task<T>>[] operations)
    {
        var gate = new TaskCompletionSource();
        var started = operations.Select(operation => Task.Run(async () =>
        {
            await gate.Task;
            return await operation();
        })).ToArray();

        gate.SetResult();
        return await Task.WhenAll(started);
    }

    /// <summary>Starts operations together, as <see cref="RunTogetherAsync{T}"/>, when they give nothing.</summary>
    /// <param name="operations">The operations.</param>
    /// <returns>A task that ends when all of them are done; it fails if one of them fails.</returns>
    public static Task RunTogetherAsync(params Func<Task>[] operations) =>
        RunTogetherAsync(operations.Select<Func<Task>, Func<Task<bool>>>(operation => async () =>
        {
            await operation();
            return true;
        }).ToArray());
}
