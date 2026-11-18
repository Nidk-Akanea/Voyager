using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Lists the trips of a group, to a member of that group.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class ListTrips(ITripRepository trips, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Lists the trips.</summary>
    /// <param name="command">Which group.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trips, in the order they were created; or why it is refused.</returns>
    public async Task<Result<IReadOnlyList<Trip>>> ExecuteAsync(Command command, CancellationToken cancellationToken) =>
        await groups.IsMemberAsync(command.Caller, command.Group, cancellationToken)
            ? Result.Success(await trips.ListByGroupAsync(command.Group, cancellationToken))
            : Result.Failure<IReadOnlyList<Trip>>(TripsErrors.NotAMember);

    #endregion Public Methods

    #region Types

    /// <summary>Which group to look into.</summary>
    /// <param name="Caller">The user who asks.</param>
    /// <param name="Group">The group.</param>
    public sealed record Command(UserId Caller, GroupId Group);

    #endregion Types
}
