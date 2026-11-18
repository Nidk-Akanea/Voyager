using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Persistence.Tests.Support;

namespace Voyager.Persistence.Tests.Trips;

[TestClass]
public sealed class TravelerRepositoryConcurrencyTests
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
    public async Task TryAdd_SameIdentifierAtTheSameTime_ExactlyOneSucceedsAndNoneThrows()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var id = TravelerId.New();
            var memberB = new TravelerBuilder().WithId(id).WithName("Member B").Build();
            var travelerA = new TravelerBuilder().WithId(id).WithName("Traveler A").Build();

            var results = await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Travelers.TryAddAsync(memberB, CancellationToken.None),
                () => _database.Travelers.TryAddAsync(travelerA, CancellationToken.None));

            results.Count(added => added).Should().Be(1, $"attempt {attempt}: exactly one creation wins");
        }
    }

    [TestMethod]
    public async Task TryAdd_TwoProfilesOfTheSameOwnerAtTheSameTime_ExactlyOneSucceedsAndNoneThrows()
    {
        for (var attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var owner = UserId.New();
            var first = new TravelerBuilder().WithOwner(owner).WithName("Traveler A").AsProfile().Build();
            var second = new TravelerBuilder().WithOwner(owner).WithName("Member B").AsProfile().Build();

            var results = await TestDatabaseSeeding.RunTogetherAsync(
                () => _database.Travelers.TryAddAsync(first, CancellationToken.None),
                () => _database.Travelers.TryAddAsync(second, CancellationToken.None));

            results.Count(added => added).Should().Be(1, $"attempt {attempt}: exactly one profile is kept");
            (await _database.Travelers.ListByOwnerAsync(owner, CancellationToken.None)).Should().ContainSingle();
        }
    }
}
