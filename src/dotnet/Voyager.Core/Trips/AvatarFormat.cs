namespace Voyager.Core.Trips;

/// <summary>
/// The formats of image an avatar may have. The numbers are what the storage keeps: never renumber, only add.
/// </summary>
public enum AvatarFormat
{
    /// <summary>A PNG image.</summary>
    Png = 0,

    /// <summary>A JPEG image.</summary>
    Jpeg = 1,

    /// <summary>A WebP image.</summary>
    WebP = 2,
}
