using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The conversions between the entities of the core and the rows of the tables, written by hand (ADR-0006). The data
/// were checked when they were written: reading them back does not check them again.
/// </summary>
internal static class TripsMapping
{
    #region Public Methods

    public static Traveler ToTraveler(TravelerRow row) =>
        Traveler.Restore(
            new TravelerId(row.Id),
            new UserId(row.OwnerId),
            row.IsProfile,
            row.Name,
            row.Nickname,
            row.BirthDate is { } date ? new LocalDate(date.Year, date.Month, date.Day) : null,
            row.AvatarContent is { } content && row.AvatarFormat is { } format ? Avatar.Restore(content, format) : null,
            row.Description);

    public static DateOnly? ToDateOnly(LocalDate? date) => date is { } day ? new DateOnly(day.Year, day.Month, day.Day) : null;

    public static TravelerRow ToRow(Traveler traveler) =>
        new()
        {
            Id = traveler.Id.Value,
            OwnerId = traveler.OwnerId.Value,
            IsProfile = traveler.IsProfile,
            Name = traveler.Name,
            Nickname = traveler.Nickname,
            BirthDate = ToDateOnly(traveler.BirthDate),
            Description = traveler.Description,
            AvatarContent = traveler.Avatar?.Content.ToArray(),
            AvatarFormat = traveler.Avatar?.Format,
        };

    public static TripRow ToRow(Trip trip) =>
        new()
        {
            Id = trip.Id.Value,
            GroupId = trip.GroupId.Value,
            CreatorId = trip.CreatorId.Value,
            Name = trip.Name,
        };

    public static Trip ToTrip(TripRow row, IEnumerable<Guid> travelers) =>
        Trip.Restore(
            new TripId(row.Id),
            new GroupId(row.GroupId),
            new UserId(row.CreatorId),
            row.Name,
            travelers.Select(id => new TravelerId(id)));

    #endregion Public Methods
}
