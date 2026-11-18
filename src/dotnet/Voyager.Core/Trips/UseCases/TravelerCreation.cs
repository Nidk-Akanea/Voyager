using NodaTime;

using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// The creation of a traveler, which a profile and any other traveler share. A creation sent again, with the same
/// identifier and the same content, works as the first time and changes nothing (ADR-0018, decision 2).
/// </summary>
internal static class TravelerCreation
{
    #region Public Methods

    /// <summary>
    /// Creates a traveler for a user.
    /// </summary>
    /// <param name="travelers">Where the travelers are kept.</param>
    /// <param name="clock">The clock of the server.</param>
    /// <param name="caller">The user who declares the traveler, and owns it.</param>
    /// <param name="id">The identifier, created by the caller.</param>
    /// <param name="isProfile">Whether it is the profile of the caller.</param>
    /// <param name="details">The texts and the date of birth, as typed.</param>
    /// <param name="avatar">The image to give it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The traveler; or why it is refused: invalid, a profile already there, or an identifier already used.</returns>
    public static async Task<Result<Traveler>> CreateAsync(
        ITravelerRepository travelers,
        IClock clock,
        UserId caller,
        TravelerId id,
        bool isProfile,
        TravelerDetails details,
        AvatarChange avatar,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<Traveler>(TripsErrors.Invalid(TripsErrors.ID_FIELD, TripsErrors.REQUIRED));
        }

        var created = Traveler.Create(id, caller, isProfile, details, avatar, Days.Today(clock));
        if (created.IsFailure)
        {
            return created;
        }

        var traveler = created.Value;
        if (await travelers.TryAddAsync(traveler, cancellationToken))
        {
            return created;
        }

        // Not kept: the identifier is used, or the user already has a profile. Sent again, the same creation is the first.
        if (await travelers.FindAsync(id, cancellationToken) is { } existing)
        {
            return existing.HasSameContentAs(traveler)
                ? Result.Success(existing)
                : Result.Failure<Traveler>(TripsErrors.IdAlreadyUsed);
        }

        return Result.Failure<Traveler>(isProfile ? TripsErrors.ProfileAlreadyExists : TripsErrors.IdAlreadyUsed);
    }

    #endregion Public Methods
}
