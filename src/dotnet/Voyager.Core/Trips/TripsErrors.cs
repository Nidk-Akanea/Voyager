using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// The expected failures of the trips and the travelers, with the stable codes the interface translates.
/// </summary>
public static class TripsErrors
{
    #region Fields

    /// <summary>The field of the identifier of a traveler or of a trip.</summary>
    public const string ID_FIELD = "id";

    /// <summary>The field of the name of a traveler or of a trip.</summary>
    public const string NAME_FIELD = "name";

    /// <summary>The field of the nickname of a traveler.</summary>
    public const string NICKNAME_FIELD = "nickname";

    /// <summary>The field of the description of a traveler.</summary>
    public const string DESCRIPTION_FIELD = "description";

    /// <summary>The field of the date of birth of a traveler.</summary>
    public const string BIRTH_DATE_FIELD = "birth-date";

    /// <summary>The field of the avatar of a traveler.</summary>
    public const string AVATAR_FIELD = "avatar";

    /// <summary>The text is empty, or only made of spaces and characters that are cleaned away.</summary>
    public const string REQUIRED = "required";

    /// <summary>The text has more characters than allowed.</summary>
    public const string TOO_LONG = "too-long";

    /// <summary>The file of an avatar is not a PNG, a JPEG or a WebP image.</summary>
    public const string NOT_AN_IMAGE = "not-an-image";

    /// <summary>The file of an avatar weighs more than allowed.</summary>
    public const string TOO_LARGE = "too-large";

    /// <summary>The date of birth is after the day the server is in.</summary>
    public const string IN_THE_FUTURE = "in-the-future";

    /// <summary>The identifier is already used by something else than what the caller sends again.</summary>
    public static readonly Error IdAlreadyUsed = new(ErrorKind.Conflict, "id-already-used");

    /// <summary>The caller is not a member of the group.</summary>
    public static readonly Error NotAMember = new(ErrorKind.NotAllowed, "not-a-member");

    /// <summary>Only the creator of a trip may do this to it.</summary>
    public static readonly Error NotTheCreator = new(ErrorKind.NotAllowed, "not-the-creator");

    /// <summary>The user already has a profile.</summary>
    public static readonly Error ProfileAlreadyExists = new(ErrorKind.Conflict, "profile-already-exists");

    /// <summary>The profile of a user is never deleted on its own.</summary>
    public static readonly Error ProfileCannotBeDeleted = new(ErrorKind.NotAllowed, "profile-cannot-be-deleted");

    /// <summary>The traveler does not exist, or does not belong to the caller.</summary>
    public static readonly Error TravelerNotFound = new(ErrorKind.NotFound, "traveler-not-found");

    /// <summary>The trip does not exist, or belongs to a group the caller is not in.</summary>
    public static readonly Error TripNotFound = new(ErrorKind.NotFound, "trip-not-found");

    #endregion Fields

    #region Public Methods

    /// <summary>An invalid field.</summary>
    /// <param name="field">The field, one of the <c>_FIELD</c> constants.</param>
    /// <param name="reason">Why, one of <see cref="REQUIRED"/>, <see cref="TOO_LONG"/>, <see cref="NOT_AN_IMAGE"/>,
    /// <see cref="TOO_LARGE"/> and <see cref="IN_THE_FUTURE"/>.</param>
    /// <returns>The error, whose code is the field and the reason, such as <c>name-required</c>.</returns>
    public static Error Invalid(string field, string reason) => new(ErrorKind.Invalid, $"{field}-{reason}", field);

    #endregion Public Methods
}
