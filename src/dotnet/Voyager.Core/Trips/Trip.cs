using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// A trip: the project a group prepares together. It belongs to a group, has a creator, and lists its travelers in the
/// order they were added. It does not change: each change makes another trip.
/// </summary>
public sealed class Trip
{
    #region Fields

    /// <summary>The most characters of a name.</summary>
    public const int MAXIMUM_NAME_LENGTH = 100;

    private readonly TravelerId[] _travelers;

    #endregion Fields

    #region Private Constructors

    private Trip(TripId id, GroupId groupId, UserId creatorId, string name, TravelerId[] travelers)
    {
        Id = id;
        GroupId = groupId;
        CreatorId = creatorId;
        Name = name;
        _travelers = travelers;
        Travelers = Array.AsReadOnly(travelers);
    }

    #endregion Private Constructors

    #region Properties

    /// <summary>The member who created the trip: the only one who deletes it.</summary>
    public UserId CreatorId { get; }

    /// <summary>The group the trip belongs to.</summary>
    public GroupId GroupId { get; }

    /// <summary>The identifier of the trip.</summary>
    public TripId Id { get; }

    /// <summary>The name.</summary>
    public string Name { get; }

    /// <summary>The travelers of the trip, once each, in the order they were added.</summary>
    public IReadOnlyList<TravelerId> Travelers { get; }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Makes a trip from what a member typed.
    /// </summary>
    /// <param name="id">The identifier, created by the caller.</param>
    /// <param name="groupId">The group the trip belongs to.</param>
    /// <param name="creatorId">The member who creates it.</param>
    /// <param name="name">The name, as typed.</param>
    /// <param name="creatorTraveler">The profile of the creator, who is a traveler of the trip from the start; null for none.</param>
    /// <returns>The trip, or why its name is refused.</returns>
    public static Result<Trip> Create(TripId id, GroupId groupId, UserId creatorId, string? name, TravelerId? creatorTraveler)
    {
        var checkedName = ValidateName(name);
        if (checkedName.IsFailure)
        {
            return Result.Failure<Trip>(checkedName.Error);
        }

        TravelerId[] travelers = creatorTraveler is { } first ? [first] : [];
        return Result.Success(new Trip(id, groupId, creatorId, checkedName.Value!, travelers));
    }

    /// <summary>
    /// Rebuilds a trip that was already checked, as the storage gives it back.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="creatorId">The creator.</param>
    /// <param name="name">The name.</param>
    /// <param name="travelers">The travelers, in the order they were added.</param>
    /// <returns>The trip.</returns>
    public static Trip Restore(TripId id, GroupId groupId, UserId creatorId, string name, IEnumerable<TravelerId> travelers) =>
        new(id, groupId, creatorId, name, [.. travelers]);

    /// <summary>Whether a traveler is in the trip.</summary>
    /// <param name="travelerId">The traveler.</param>
    /// <returns>True when it is.</returns>
    public bool Includes(TravelerId travelerId) => Array.IndexOf(_travelers, travelerId) >= 0;

    /// <summary>
    /// Gives the trip another name.
    /// </summary>
    /// <param name="name">The new name, as typed.</param>
    /// <returns>The renamed trip, with the same identifier, creator and travelers; or why the name is refused.</returns>
    public Result<Trip> Rename(string? name)
    {
        var checkedName = ValidateName(name);
        return checkedName.IsFailure
            ? Result.Failure<Trip>(checkedName.Error)
            : Result.Success(new Trip(Id, GroupId, CreatorId, checkedName.Value!, _travelers));
    }

    /// <summary>Adds a traveler, after the others.</summary>
    /// <param name="travelerId">The traveler.</param>
    /// <returns>The trip with the traveler; this same trip when it was already there.</returns>
    public Trip WithTraveler(TravelerId travelerId) =>
        Includes(travelerId) ? this : new Trip(Id, GroupId, CreatorId, Name, [.. _travelers, travelerId]);

    /// <summary>Takes a traveler out; the others keep their order.</summary>
    /// <param name="travelerId">The traveler.</param>
    /// <returns>The trip without the traveler; this same trip when it was not there.</returns>
    public Trip WithoutTraveler(TravelerId travelerId) =>
        Includes(travelerId) ? new Trip(Id, GroupId, CreatorId, Name, [.. _travelers.Where(other => other != travelerId)]) : this;

    #endregion Public Methods

    #region Private Methods

    private static Result<string?> ValidateName(string? name) =>
        TextField.Validate(name, TripsErrors.NAME_FIELD, MAXIMUM_NAME_LENGTH, required: true);

    #endregion Private Methods
}
