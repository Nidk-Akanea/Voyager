namespace Voyager.GroupServer;

/// <summary>
/// A file of the interface part of a plugin, as the server publishes it for the desktop app to download.
/// </summary>
/// <param name="Path">Path relative to the folder of the plugin, with forward slashes, such as <c>ui/index.js</c>.</param>
/// <param name="Sha256">SHA-256 fingerprint of the file, in lower-case hexadecimal.</param>
/// <param name="Size">Size of the file, in bytes.</param>
public sealed record InterfaceFileEntry(string Path, string Sha256, long Size);
