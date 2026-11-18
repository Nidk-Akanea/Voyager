using AwesomeAssertions;

using Voyager.Persistence.Tests.Support;

namespace Voyager.Persistence.Tests.Trips;

[TestClass]
public sealed class TripRepositoryConcurrencyTests
{
    // The same race is run several times: a single run could win by luck.
    private const int ATTEMPTS = 15;

    private static TestDatabase _database = null!;

    [ClassCleanup]
    public static async Task CleanupAsync() => await _database.DisposeAsync();

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _database = await TestDatabase.CreateAsync();
    }

    [TestMethod]
    public async Task AddTraveler_DifferentTravelersAtTheSameTime_KeepsBoth()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var travelerA = await _database.SeedAsync(new TravelerBuilder().WithName("Traveler A").Build());
            var memberB = await _database.SeedAsync(new TravelerBuilder().WithName("Member B").Build());
            var trip = await _database.SeedAsync(new TripBuilder().Build());

            await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None),
                () => _database.Trips.AddTravelerAsync(trip.Id, memberB.Id, CancellationToken.None));

            (await _database.TravelersOfAsync(trip.Id)).Should().BeEquivalentTo([travelerA.Id, memberB.Id], $"attempt {attempt}: both additions stay");
        }
    }

    [TestMethod]
    public async Task AddTraveler_SameTravelerAtTheSameTime_KeepsItOnceAndNoneThrows()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var travelerA = await _database.SeedAsync(new TravelerBuilder().WithName("Traveler A").Build());
            var trip = await _database.SeedAsync(new TripBuilder().Build());

            await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None),
                () => _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None));

            (await _database.TravelersOfAsync(trip.Id)).Should().Equal(travelerA.Id);
        }
    }

    [TestMethod]
    public async Task Rename_AtTheSameTimeAsAnAddition_KeepsBothChanges()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var travelerA = await _database.SeedAsync(new TravelerBuilder().WithName("Traveler A").Build());
            var trip = await _database.SeedAsync(new TripBuilder().WithName("Trip to Lisbon").Build());

            await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Trips.RenameAsync(trip.Id, "Trip to Porto", CancellationToken.None),
                () => _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None));

            var found = await _database.Trips.FindAsync(trip.Id, CancellationToken.None);
            found!.Name.Should().Be("Trip to Porto", $"attempt {attempt}: the renaming stays");
            found.Travelers.Should().Equal(travelerA.Id);
        }
    }

    [TestMethod]
    public async Task TryAdd_SameIdentifierAtTheSameTime_ExactlyOneSucceedsAndNoneThrows()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var id = new TripBuilder().Build().Id;
            var lisbon = new TripBuilder().WithId(id).WithName("Trip to Lisbon").Build();
            var porto = new TripBuilder().WithId(id).WithName("Trip to Porto").Build();

            var results = await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Trips.TryAddAsync(lisbon, CancellationToken.None),
                () => _database.Trips.TryAddAsync(porto, CancellationToken.None));

            results.Count(added => added).Should().Be(1, $"attempt {attempt}: exactly one creation wins");
        }
    }
}
