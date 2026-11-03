using System.IO.Compression;

using AwesomeAssertions;

using Voyager.PluginHost.Packages;

namespace Voyager.PluginHost.Tests.Packages;

[TestClass]
public sealed class PackageEntryRulesTests
{
    #region Fields

    // The Unix mode of a symbolic link (type 0xA000, rights 0777), as zip tools keep it in the high bits.
    private const int UNIX_SYMBOLIC_LINK = unchecked((int)0xA1FF0000);

    // The Unix mode of a plain file (type 0x8000, rights 0644).
    private const int UNIX_REGULAR_FILE = unchecked((int)0x81A40000);

    #endregion Fields

    #region Private Methods

    private static ZipArchiveEntry EntryWithAttributes(ZipArchive archive, int externalAttributes)
    {
        var entry = archive.CreateEntry("data/route.txt");
        entry.ExternalAttributes = externalAttributes;
        return entry;
    }

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow("plugin.json", "plugin.json")]
    [DataRow("ui/index.js", "ui/index.js")]
    [DataRow("data/maps/old-town.geojson", "data/maps/old-town.geojson")]
    [DataRow("i18n/fr/messages.ftl", "i18n/fr/messages.ftl")]
    [DataRow("data/console.txt", "data/console.txt", DisplayName = "a name that only starts like a device")]
    [DataRow("data/com10.txt", "data/com10.txt", DisplayName = "com10 is no device")]
    [DataRow("data/notes~draft.txt", "data/notes~draft.txt", DisplayName = "a tilde followed by a letter")]
    [DataRow("data/notes.txt~", "data/notes.txt~", DisplayName = "a final tilde")]
    [DataRow("data/com\u2074.txt", "data/com\u2074.txt", DisplayName = "com with a superscript four is no device")]
    public void SafePath_PlainRelativeName_ReturnsIt(string entryName, string expected)
    {
        var path = PackageEntryRules.SafePath(entryName, out var isFolder);

        path.Should().Be(expected);
        isFolder.Should().BeFalse();
    }

    [TestMethod]
    public void SafePath_FolderEntry_ReturnsItWithoutTheSlash()
    {
        var path = PackageEntryRules.SafePath("data/maps/", out var isFolder);

        path.Should().Be("data/maps");
        isFolder.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("../x", DisplayName = "zip slip")]
    [DataRow("data/../../x", DisplayName = "zip slip from a folder")]
    [DataRow("data/./x", DisplayName = "dot segment")]
    [DataRow("data//x", DisplayName = "empty segment")]
    [DataRow("/x", DisplayName = "absolute path")]
    [DataRow("C:/x", DisplayName = "drive")]
    [DataRow("data\\x.txt", DisplayName = "backslash")]
    [DataRow("..\\x", DisplayName = "zip slip with a backslash")]
    [DataRow("data/a.txt:hidden", DisplayName = "alternate data stream")]
    [DataRow("data/a*.txt", DisplayName = "wildcard")]
    [DataRow("data/a\u0001.txt", DisplayName = "control character")]
    [DataRow("con", DisplayName = "console at the root")]
    [DataRow("data/nul.txt", DisplayName = "nul with an extension")]
    [DataRow("ui/NUL.js", DisplayName = "device name in upper case")]
    [DataRow("data/com0", DisplayName = "com0")]
    [DataRow("data/deep/lpt9.bin", DisplayName = "lpt9 in a sub-folder")]
    [DataRow("data/aux.tar.gz", DisplayName = "device name with two extensions")]
    [DataRow("data/prn /x.txt", DisplayName = "device folder with a trailing space")]
    [DataRow("data/conin$", DisplayName = "console input")]
    [DataRow("data/route.", DisplayName = "trailing dot")]
    [DataRow("data/route ", DisplayName = "trailing space")]
    [DataRow("data. /x.txt", DisplayName = "folder with a trailing space")]
    [DataRow("ui/LONGFI~1.JS", DisplayName = "8.3 short name of a file")]
    [DataRow("data/PROGRA~2/rates.csv", DisplayName = "8.3 short name of a folder")]
    [DataRow("data/route~10", DisplayName = "8.3 short name without an extension")]
    [DataRow("ui/com\u00B9.js", DisplayName = "com with a superscript one")]
    [DataRow("data/COM\u00B2", DisplayName = "com with a superscript two in upper case")]
    [DataRow("data/lpt\u00B3.txt", DisplayName = "lpt with a superscript three")]
    [DataRow("data/lpt\u00B9/rates.csv", DisplayName = "lpt with a superscript one as a folder")]
    [DataRow("", DisplayName = "empty name")]
    [DataRow("/", DisplayName = "root folder")]
    public void SafePath_NameThatIsNotSafeOnWindows_ReturnsNull(string entryName)
    {
        var path = PackageEntryRules.SafePath(entryName, out _);

        path.Should().BeNull();
    }

    [TestMethod]
    public void SafePath_SegmentLongerThan255Characters_ReturnsNull()
    {
        var path = PackageEntryRules.SafePath($"data/{new string('a', 256)}", out _);

        path.Should().BeNull();
    }

    [TestMethod]
    public void SafePath_PathLongerThan1024Characters_ReturnsNull()
    {
        var name = string.Join('/', Enumerable.Repeat(new string('a', 200), 6));

        var path = PackageEntryRules.SafePath(name, out _);

        path.Should().BeNull();
    }

    [TestMethod]
    [DataRow(UNIX_SYMBOLIC_LINK, true, DisplayName = "Unix symbolic link")]
    [DataRow((int)FileAttributes.ReparsePoint, true, DisplayName = "Windows reparse point")]
    [DataRow(UNIX_REGULAR_FILE, false, DisplayName = "Unix plain file")]
    [DataRow((int)FileAttributes.Archive, false, DisplayName = "Windows plain file")]
    [DataRow(0, false, DisplayName = "no attributes")]
    public void IsLink_ExternalAttributes_TellsALinkFromAFile(int externalAttributes, bool expected)
    {
        using var archive = new ZipArchive(new MemoryStream(), ZipArchiveMode.Create);
        var entry = EntryWithAttributes(archive, externalAttributes);

        var isLink = PackageEntryRules.IsLink(entry);

        isLink.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("plugin.json")]
    [DataRow("signature.json")]
    [DataRow("Fixture.Itinerary.dll")]
    [DataRow("Fixture.Itinerary.DLL")]
    [DataRow("Fixture.Itinerary.pdb")]
    [DataRow("Fixture.Itinerary.xml")]
    [DataRow("Fixture.Itinerary.deps.json")]
    [DataRow("ui/index.js")]
    [DataRow("ui/views/map.mjs")]
    [DataRow("ui/styles.css")]
    [DataRow("ui/strings.json")]
    [DataRow("ui/icon.svg")]
    [DataRow("ui/icon.png")]
    [DataRow("i18n/en/messages.ftl")]
    [DataRow("data/rates.csv")]
    [DataRow("data/tiles/old-town.mbtiles")]
    [DataRow("data/tool.exe", DisplayName = "data of any kind, even an executable")]
    [DataRow("data/native.dll", DisplayName = "data of any kind, even a library")]
    public void IsAllowedFile_FileOfTheAllowedList_IsAllowed(string path)
    {
        var allowed = PackageEntryRules.IsAllowedFile(path);

        allowed.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("setup.exe", DisplayName = "executable at the root")]
    [DataRow("readme.txt", DisplayName = "text at the root")]
    [DataRow("other.json", DisplayName = "JSON at the root other than the manifest")]
    [DataRow("Fixture.Itinerary.runtimeconfig.json", DisplayName = "runtime configuration")]
    [DataRow("ui/x.exe", DisplayName = "executable in ui")]
    [DataRow("ui/page.html", DisplayName = "HTML in ui")]
    [DataRow("ui/photo.jpg", DisplayName = "JPEG in ui")]
    [DataRow("i18n/fr/messages.js", DisplayName = "script in i18n")]
    [DataRow("bin/Fixture.Itinerary.dll", DisplayName = "other top folder")]
    [DataRow("runtimes/win-x64/native/route.dll", DisplayName = "native runtime folder")]
    [DataRow("UI/index.js", DisplayName = "ui in upper case")]
    public void IsAllowedFile_FileOutsideTheAllowedList_IsNotAllowed(string path)
    {
        var allowed = PackageEntryRules.IsAllowedFile(path);

        allowed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("ui", true)]
    [DataRow("ui/views", true)]
    [DataRow("i18n", true)]
    [DataRow("data", true)]
    [DataRow("data/tiles/old-town", true)]
    [DataRow("bin", false)]
    [DataRow("runtimes", false)]
    public void IsAllowedFolder_Folder_IsAllowedOnlyUnderTheThreeFolders(string path, bool expected)
    {
        var allowed = PackageEntryRules.IsAllowedFolder(path);

        allowed.Should().Be(expected);
    }

    #endregion Public Methods
}
