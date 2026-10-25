using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

using Voyager.Contracts.Diagnostics.V1;

namespace Voyager.LocalService.Tests;

[TestClass]
public sealed class DiagnosticsGrpcServiceTests
{
    private static readonly Instant _start = Instant.FromUtc(2026, 3, 14, 9, 30);

    [TestMethod]
    public async Task GetServiceInfo_WithLocalToken_ReturnsVersionStartAndAvailableDatabase()
    {
        using var factory = new LocalServiceFactory(clock: new FakeClock(_start));
        using var channel = factory.CreateChannel();
        var client = new DiagnosticsService.DiagnosticsServiceClient(channel);

        var response = await client.GetServiceInfoAsync(
            new GetServiceInfoRequest(),
            LocalServiceFactory.TokenHeader(LocalServiceFactory.LOCAL_TOKEN));

        response.Version.Should().NotBeNullOrWhiteSpace().And.NotContain("+");
        response.Runtime.Should().StartWith(".NET");
        response.Environment.Should().Be("Development");
        response.StartedAt.ToDateTimeOffset().Should().Be(_start.ToDateTimeOffset());
        response.DatabaseStatus.Should().Be(DatabaseStatus.Available);
        File.Exists(factory.DatabasePath).Should().BeTrue("the migration creates the SQLite file");
    }

    [TestMethod]
    public async Task GetServiceInfo_WhenDatabaseCannotBeCreated_ReportsUnavailableDatabase()
    {
        // A database "folder" that is a file: the database can be neither created nor migrated.
        var notAFolder = Path.Combine(Path.GetTempPath(), $"voyager-tests-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(notAFolder, "not a folder");
        try
        {
            using var factory = new LocalServiceFactory(databasePath: Path.Combine(notAFolder, "voyager.db"));
            using var channel = factory.CreateChannel();
            var client = new DiagnosticsService.DiagnosticsServiceClient(channel);

            var response = await client.GetServiceInfoAsync(
                new GetServiceInfoRequest(),
                LocalServiceFactory.TokenHeader(LocalServiceFactory.LOCAL_TOKEN));

            response.DatabaseStatus.Should().Be(DatabaseStatus.Unavailable);
        }
        finally
        {
            File.Delete(notAFolder);
        }
    }
}
