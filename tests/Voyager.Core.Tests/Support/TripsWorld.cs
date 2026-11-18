using NodaTime;
using NodaTime.Testing;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// The setting every test of the trips starts from: a group with two members (the user of the test and Member B),
/// another group with Member C, the fake storage, and a clock fixed on 2026-11-18.
/// </summary>
internal sealed class TripsWorld
{
    #region Constructors

    public TripsWorld()
    {
        Travelers = new InMemoryTravelerRepository(Trips);
        Groups.Add(Me, Group);
        Groups.Add(MemberB, Group);
        Groups.Add(MemberC, OtherGroup);
    }

    #endregion Constructors

    #region Properties

    public static LocalDate Today => new(2026, 11, 18);

    public FakeClock Clock { get; } = new(Instant.FromUtc(2026, 11, 18, 12, 0));

    public FakeGroupMembership Groups { get; } = new();

    public GroupId Group { get; } = GroupId.New();

    public UserId Me { get; } = UserId.New();

    public UserId MemberB { get; } = UserId.New();

    public UserId MemberC { get; } = UserId.New();

    public GroupId OtherGroup { get; } = GroupId.New();

    public InMemoryTravelerRepository Travelers { get; }

    public InMemoryTripRepository Trips { get; } = new();

    #endregion Properties

    #region Public Methods

    public Traveler AddTraveler(TravelerBuilder builder)
    {
        var traveler = builder.Build();
        Travelers.Add(traveler);
        return traveler;
    }

    public Traveler AddTravelerOf(UserId owner, string name = "Traveler A") =>
        AddTraveler(new TravelerBuilder().WithOwner(owner).WithName(name));

    public Trip AddTrip(TripBuilder builder)
    {
        var trip = builder.Build();
        Trips.Add(trip);
        return trip;
    }

    /// <summary>A trip of the main group, created by the user of the test.</summary>
    public Trip AddTripOfMyGroup(params TravelerId[] travelers) =>
        AddTrip(new TripBuilder().WithGroup(Group).WithCreator(Me).WithTravelers(travelers));

    #endregion Public Methods
}
