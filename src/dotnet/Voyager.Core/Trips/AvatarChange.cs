namespace Voyager.Core.Trips;

/// <summary>
/// What a caller asks for the avatar of a traveler: keep it, remove it, or replace it with an image.
/// </summary>
public readonly record struct AvatarChange
{
    #region Private Constructors

    private AvatarChange(AvatarChangeKind kind, ReadOnlyMemory<byte> content)
    {
        Kind = kind;
        Content = content;
    }

    #endregion Private Constructors

    #region Properties

    /// <summary>The bytes of the new image, for <see cref="AvatarChangeKind.Replace"/>.</summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>Leave the avatar as it is.</summary>
    public static AvatarChange Keep => default;

    /// <summary>What is asked.</summary>
    public AvatarChangeKind Kind { get; }

    /// <summary>Take the avatar away.</summary>
    public static AvatarChange Remove => new(AvatarChangeKind.Remove, default);

    #endregion Properties

    #region Public Methods

    /// <summary>Put another image in place of the avatar.</summary>
    /// <param name="content">The bytes of the file the user chose.</param>
    /// <returns>The change.</returns>
    public static AvatarChange Replace(ReadOnlyMemory<byte> content) => new(AvatarChangeKind.Replace, content);

    #endregion Public Methods
}
