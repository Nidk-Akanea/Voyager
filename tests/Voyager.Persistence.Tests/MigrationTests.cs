using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Voyager.Persistence.Tests.Support;

namespace Voyager.Persistence.Tests;

[TestClass]
public sealed class MigrationTests
{
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
    public async Task Migrate_EmptyDatabase_CreatesTheThreeTables()
    {
        var tables = await _database.ColumnAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");

        tables.Should().Contain(["travelers", "trips", "trip_travelers"]);
    }

    [TestMethod]
    public async Task Migrate_EmptyDatabase_KeepsTheMigrationInTheHistory()
    {
        await using var context = _database.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();
        var applied = await context.Database.GetAppliedMigrationsAsync();

        pending.Should().BeEmpty();
        applied.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task Migrate_EmptyDatabase_IsUpToDateWithTheModel()
    {
        await using var context = _database.CreateContext();

        var hasPendingChanges = context.Database.HasPendingModelChanges();

        hasPendingChanges.Should().BeFalse("the migration must be remade when the model changes");
    }

    [TestMethod]
    public async Task Migrate_EmptyDatabase_CreatesTheUniqueProfileIndexOfTheOwner()
    {
        var definitions = await _database.ColumnAsync(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'ux_travelers_profile_per_owner'");

        var definition = definitions.Should().ContainSingle().Subject;
        definition.Should().Contain("UNIQUE").And.Contain("owner_id").And.Contain("WHERE");
    }

    [TestMethod]
    public async Task Migrate_AppliedTwice_ChangesNothing()
    {
        await using var context = _database.CreateContext();

        var again = async () => await context.Database.MigrateAsync();

        await again.Should().NotThrowAsync();
    }
}
