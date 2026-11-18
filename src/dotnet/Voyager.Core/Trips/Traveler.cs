using NodaTime;

using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// A person who makes trips, reusable from one trip to the next. It belongs to the user who declared it: only that user
/// changes or deletes it. The profile of a user is a traveler marked as theirs.
/// </summary>
public sealed class Traveler
{
    #region Fields

    /// <summary>The most characters of a name.</summary>
    public const int MAXIMUM_NAME_LENGTH = 100;

    /// <summary>The most characters of a nickname.</summary>
    public const int MAXIMUM_NICKNAME_LENGTH = 50;

    /// <summary>The most characters of a description.</summary>
    public const int MAXIMUM_DESCRIPTION_LENGTH = 500;

    #endregion Fields

    #region Private Constructors

    private Traveler(
        TravelerId id,
        UserId ownerId,
        bool isProfile,
        string name,
        string? nickname,
        LocalDate? birthDate,
        Avatar? avatar,
        string? description)
    {
        Id = id;
        OwnerId = ownerId;
        IsProfile = isProfile;
        Name = name;
        Nickname = nickname;
        BirthDate = birthDate;
        Avatar = avatar;
        Description = description;
    }

    #endregion Private Constructors

    #region Properties

    /// <summary>The image of the traveler, if it has one.</summary>
    public Avatar? Avatar { get; }

    /// <summary>The date of birth, if it is known.</summary>
    public LocalDate? BirthDate { get; }

    /// <summary>The description, if there is one.</summary>
    public string? Description { get; }

    /// <summary>What the screens call the traveler: the nickname when there is one, else the name.</summary>
    public string DisplayName => Nickname ?? Name;

    /// <summary>The identifier of the traveler.</summary>
    public TravelerId Id { get; }

    /// <summary>Whether this is the profile of its owner: the traveler the user is.</summary>
    public bool IsProfile { get; }

    /// <summary>The name.</summary>
    public string Name { get; }

    /// <summary>The nickname, if there is one.</summary>
    public string? Nickname { get; }

    /// <summary>The user who declared the traveler.</summary>
    public UserId OwnerId { get; }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Makes a traveler from what a user typed.
    /// </summary>
    /// <param name="id">The identifier, created by the caller.</param>
    /// <param name="ownerId">The user who declares the traveler.</param>
    /// <param name="isProfile">Whether it is the profile of that user.</param>
    /// <param name="details">The texts and the date of birth, as typed.</param>
    /// <param name="avatar">The image to give it: <see cref="AvatarChange.Keep"/> for none.</param>
    /// <param name="today">The day the server is in (UTC), against which a date of birth is checked.</param>
    /// <returns>The traveler, or the first field that is refused, with the reason.</returns>
    public static Result<Traveler> Create(
        TravelerId id,
        UserId ownerId,
        bool isProfile,
        TravelerDetails details,
        AvatarChange avatar,
        LocalDate today) =>
        Build(id, ownerId, isProfile, details, avatar, currentAvatar: null, today);

    /// <summary>
    /// Rebuilds a traveler that was already checked, as the storage gives it back.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ownerId">The owner.</param>
    /// <param name="isProfile">Whether it is the profile of its owner.</param>
    /// <param name="name">The name.</param>
    /// <param name="nickname">The nickname, or null.</param>
    /// <param name="birthDate">The date of birth, or null.</param>
    /// <param name="avatar">The avatar, or null.</param>
    /// <param name="description">The description, or null.</param>
    /// <returns>The traveler.</returns>
    public static Traveler Restore(
        TravelerId id,
        UserId ownerId,
        bool isProfile,
        string name,
        string? nickname,
        LocalDate? birthDate,
        Avatar? avatar,
        string? description) =>
        new(id, ownerId, isProfile, name, nickname, birthDate, avatar, description);

    /// <summary>Whether another traveler holds the same data, whichever its identifier: what tells a creation sent again.</summary>
    /// <param name="other">The other traveler.</param>
    /// <returns>True when owner, profile mark, texts, date of birth and avatar are the same.</returns>
    public bool HasSameContentAs(Traveler other) =>
        OwnerId == other.OwnerId
        && IsProfile == other.IsProfile
        && Name == other.Name
        && Nickname == other.Nickname
        && BirthDate == other.BirthDate
        && Description == other.Description
        && (Avatar is null ? other.Avatar is null : other.Avatar is not null && Avatar.HasSameContentAs(other.Avatar));

    /// <summary>
    /// Changes what is known of the traveler. The traveler does not change: this makes another one.
    /// </summary>
    /// <param name="details">The new texts and date of birth, as typed.</param>
    /// <param name="avatar">What to do with the avatar.</param>
    /// <param name="today">The day the server is in (UTC).</param>
    /// <returns>The changed traveler, with the same identifier, owner and profile mark; or the first field refused.</returns>
    public Result<Traveler> Update(TravelerDetails details, AvatarChange avatar, LocalDate today) =>
        Build(Id, OwnerId, IsProfile, details, avatar, Avatar, today);

    #endregion Public Methods

    #region Private Methods

    private static Result<Traveler> Build(
        TravelerId id,
        UserId ownerId,
        bool isProfile,
        TravelerDetails details,
        AvatarChange avatarChange,
        Avatar? currentAvatar,
        LocalDate today)
    {
        var name = TextField.Validate(details.Name, TripsErrors.NAME_FIELD, MAXIMUM_NAME_LENGTH, required: true);
        if (name.IsFailure)
        {
            return Result.Failure<Traveler>(name.Error);
        }

        var nickname = TextField.Validate(details.Nickname, TripsErrors.NICKNAME_FIELD, MAXIMUM_NICKNAME_LENGTH, required: false);
        if (nickname.IsFailure)
        {
            return Result.Failure<Traveler>(nickname.Error);
        }

        var description = TextField.Validate(
            details.Description,
            TripsErrors.DESCRIPTION_FIELD,
            MAXIMUM_DESCRIPTION_LENGTH,
            required: false,
            keepLineBreaks: true);
        if (description.IsFailure)
        {
            return Result.Failure<Traveler>(description.Error);
        }

        // One day of margin: a user whose time zone is ahead of UTC may type a date that is still tomorrow for the server.
        if (details.BirthDate is { } birthDate && birthDate > today.PlusDays(1))
        {
            return Result.Failure<Traveler>(TripsErrors.Invalid(TripsErrors.BIRTH_DATE_FIELD, TripsErrors.IN_THE_FUTURE));
        }

        var avatar = ChooseAvatar(avatarChange, currentAvatar);
        if (avatar.IsFailure)
        {
            return Result.Failure<Traveler>(avatar.Error);
        }

        return Result.Success(new Traveler(id, ownerId, isProfile, name.Value!, nickname.Value, details.BirthDate, avatar.Value, description.Value));
    }

    private static Result<Avatar?> ChooseAvatar(AvatarChange change, Avatar? current)
    {
        switch (change.Kind)
        {
            case AvatarChangeKind.Replace:
                var created = Avatar.Create(change.Content);
                return created.IsFailure ? Result.Failure<Avatar?>(created.Error) : Result.Success<Avatar?>(created.Value);

            case AvatarChangeKind.Remove:
                return Result.Success<Avatar?>(null);

            default:
                return Result.Success(current);
        }
    }

    #endregion Private Methods
}
