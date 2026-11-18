using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// The image of a traveler: kept as it was chosen, with no retouching. Its format is recognized by what the file
/// holds, never by its name.
/// </summary>
public sealed class Avatar
{
    #region Fields

    /// <summary>The most bytes an avatar may weigh: 2 MiB.</summary>
    public const int MAXIMUM_BYTES = 2 * 1024 * 1024;

    private const int WEBP_SIGNATURE_OFFSET = 8;

    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] _jpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _riffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] _webpSignature = [0x57, 0x45, 0x42, 0x50];

    private readonly byte[] _content;

    #endregion Fields

    #region Private Constructors

    private Avatar(byte[] content, AvatarFormat format)
    {
        _content = content;
        Format = format;
    }

    #endregion Private Constructors

    #region Properties

    /// <summary>The bytes of the image.</summary>
    public ReadOnlyMemory<byte> Content => _content;

    /// <summary>The format of the image.</summary>
    public AvatarFormat Format { get; }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Makes the avatar of an image.
    /// </summary>
    /// <param name="content">The bytes of the file the user chose.</param>
    /// <returns>The avatar, or why the file is refused: too large, or not an image of an allowed format.</returns>
    public static Result<Avatar> Create(ReadOnlyMemory<byte> content)
    {
        if (content.Length > MAXIMUM_BYTES)
        {
            return Result.Failure<Avatar>(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
        }

        return Detect(content.Span) is { } format
            ? Result.Success(new Avatar(content.ToArray(), format))
            : Result.Failure<Avatar>(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    /// <summary>
    /// Rebuilds an avatar that was already checked, as the storage gives it back.
    /// </summary>
    /// <param name="content">The bytes of the image, which the avatar takes as they are: the caller no longer writes to them.</param>
    /// <param name="format">Its format.</param>
    /// <returns>The avatar.</returns>
    public static Avatar Restore(byte[] content, AvatarFormat format) => new(content, format);

    /// <summary>Whether another avatar is the same image.</summary>
    /// <param name="other">The other avatar.</param>
    /// <returns>True when both have the same format and the same bytes.</returns>
    public bool HasSameContentAs(Avatar other) => Format == other.Format && _content.AsSpan().SequenceEqual(other._content);

    #endregion Public Methods

    #region Private Methods

    private static AvatarFormat? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(_pngSignature))
        {
            return AvatarFormat.Png;
        }

        if (content.StartsWith(_jpegSignature))
        {
            return AvatarFormat.Jpeg;
        }

        // A WebP file is a RIFF container whose type, after the size, is "WEBP".
        if (content.StartsWith(_riffSignature)
            && content.Length >= WEBP_SIGNATURE_OFFSET + _webpSignature.Length
            && content[WEBP_SIGNATURE_OFFSET..].StartsWith(_webpSignature))
        {
            return AvatarFormat.WebP;
        }

        return null;
    }

    #endregion Private Methods
}
