namespace Voyager.PluginHost.Packages;

/// <summary>
/// The CRC-32 a zip archive keeps for each entry (IEEE 802.3, the polynomial of the zip format). Checked on the
/// extracted bytes: .NET stops reading an entry at the size the archive announces, so an entry that holds more, or
/// other bytes, than it says would otherwise be installed cut short or damaged without anyone knowing.
/// </summary>
internal sealed class Crc32
{
    #region Fields

    private const uint POLYNOMIAL = 0xEDB88320;

    private static readonly uint[] _table = CreateTable();

    private uint _value = 0xFFFFFFFF;

    #endregion Fields

    #region Properties

    /// <summary>The CRC-32 of the bytes added so far.</summary>
    public uint Value => ~_value;

    #endregion Properties

    #region Private Methods

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ POLYNOMIAL : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Adds bytes to the check.
    /// </summary>
    /// <param name="bytes">The next bytes of the entry.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            _value = _table[(_value ^ value) & 0xFF] ^ (_value >> 8);
        }
    }

    #endregion Public Methods
}
