using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Creates a trip in a group the caller belongs to. The caller is its creator and, when they have a profile, its first
/// traveler. A creation sent again, with the same identifier and the same content, works as the first time.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class CreateTrip(ITripRepository trips, ITravelerRepository travelers, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Creates the trip.</summary>
    /// <param name="command">What the member typed.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trip; or why it is refused: not a member, invalid name, or an identifier already used.</returns>
    public async Task<Result<Trip>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        if (!await groups.IsMemberAsync(command.Caller, command.Group, cancellationToken))
        {
            return Result.Failure<Trip>(TripsErrors.NotAMember);
        }

        if (command.Id.Value == Guid.Empty)
        {
            return Result.Failure<Trip>(TripsErrors.Invalid(TripsErrors.ID_FIELD, TripsErrors.REQUIRED));
        }

        var profile = await travelers.FindProfileAsync(command.Caller, cancellationToken);
        var created = Trip.Create(command.Id, command.Group, command.Caller, command.Name, profile?.Id);
        if (created.IsFailure)
        {
            return created;
        }

        var trip = created.Value;
        if (await trips.TryAddAsync(trip, cancellationToken))
        {
            return created;
        }

        // Not kept: the identifier is used. Sent again, the same group, creator and name are the same creation.
        var existing = await trips.FindAsync(command.Id, cancellationToken);
        return existing is not null && existing.GroupId == trip.GroupId && existing.CreatorId == trip.CreatorId && existing.Name == trip.Name
            ? Result.Success(existing)
            : Result.Failure<Trip>(TripsErrors.IdAlreadyUsed);
    }

    #endregion Public Methods

    #region Types

    /// <summary>What the member typed.</summary>
    /// <param name="Caller">The member, who becomes the creator.</param>
    /// <param name="Id">The identifier of the trip, created by the caller.</param>
    /// <param name="Group">The group of the trip.</param>
    /// <param name="Name">The name, as typed.</param>
    public sealed record Command(UserId Caller, TripId Id, GroupId Group, string? Name);

    #endregion Types
}
