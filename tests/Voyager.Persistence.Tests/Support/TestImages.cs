namespace Voyager.Persistence.Tests.Support;

/// <summary>Made-up image files: the right signature at the start, then zeros. No real picture, no real person.</summary>
internal static class TestImages
{
    public static byte[] Jpeg(int length = 64) => Pad([0xFF, 0xD8, 0xFF, 0xE0], length);

    public static byte[] Png(int length = 64) => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], length);

    /// <summary>A PNG signature followed by a repeating pattern instead of zeros, so that a truncation or a shift shows.</summary>
    public static byte[] PngWithPattern(int length)
    {
        var content = Png(length);
        for (var index = 8; index < content.Length; index++)
        {
            content[index] = (byte)(index % 251);
        }

        return content;
    }

    private static byte[] Pad(byte[] header, int length)
    {
        var content = new byte[Math.Max(length, header.Length)];
        header.CopyTo(content, 0);
        return content;
    }
}
