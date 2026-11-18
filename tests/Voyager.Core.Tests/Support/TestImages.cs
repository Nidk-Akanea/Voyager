using System.Text;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// Made-up image files: the right signature at the start, then filler. No real picture, no real person.
/// </summary>
internal static class TestImages
{
    #region Public Methods

    public static byte[] Jpeg(int length = 64) => Pad([0xFF, 0xD8, 0xFF, 0xE0], length);

    public static byte[] Png(int length = 64) => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], length);

    public static byte[] Text(string text = "This is a text file, not an image.") => Encoding.UTF8.GetBytes(text);

    /// <summary>A RIFF container whose type is <paramref name="type" />, "WEBP" for a real WebP file.</summary>
    public static byte[] WebP(int length = 64, string type = "WEBP") =>
        Pad([.. "RIFF"u8, 0x00, 0x00, 0x00, 0x00, .. Encoding.ASCII.GetBytes(type)], length);

    #endregion Public Methods

    #region Private Methods

    private static byte[] Pad(byte[] header, int length)
    {
        var content = new byte[Math.Max(length, header.Length)];
        header.CopyTo(content, 0);
        return content;
    }

    #endregion Private Methods
}
