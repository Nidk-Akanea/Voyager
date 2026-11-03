using System.Buffers.Binary;
using System.Text;

using AwesomeAssertions;

using Voyager.PluginHost.Packages;
using Voyager.Sdk;

namespace Voyager.PluginHost.Tests.Packages;

[TestClass]
public sealed class PluginPackageExtractorTests
{
    #region Fields

    private const string VIEW_ID = "fixture-view";
    private const string STAGED_NAME = "5ea7ed0c0ffee5ea7ed0c0ffee5ea7ed.voyagerplugin";
    private const string ITINERARY_SOURCE = "itinerary-source";

    // The Unix mode of a symbolic link (type 0xA000, rights 0777), as zip tools keep it in the high bits.
    private const int UNIX_SYMBOLIC_LINK = unchecked((int)0xA1FF0000);

    private static readonly ContractSupport _contracts = new(
        [
            new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
            new ContractVersion(ExtensionPoints.DIAGNOSTIC_FACT_SOURCE, 1, ContractVersionState.Supported),
            new ContractVersion(ITINERARY_SOURCE, 1, ContractVersionState.Obsolete),
            new ContractVersion(ITINERARY_SOURCE, 2, ContractVersionState.Deprecated),
            new ContractVersion(ITINERARY_SOURCE, 3, ContractVersionState.Supported),
        ],
        [ContractVersions.PLUGIN]);

    #endregion Fields

    #region Private Methods

    private static string DestinationOf(UserPluginFolder user) => Path.Combine(user.StagingDirectory, ".extract-test");

    private static PackageExtraction Extract(UserPluginFolder user, IEnumerable<PackageEntry> entries, bool everything = true)
    {
        var package = TestPackage.Stage(user, STAGED_NAME, entries);
        return PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything);
    }

    private static PackageExtraction ExtractRaw(UserPluginFolder user, IEnumerable<RawPackageEntry> entries, bool everything = true)
    {
        var package = TestPackage.WriteRaw(Path.Combine(user.StagingDirectory, STAGED_NAME), entries);
        return PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything);
    }

    private static List<RawPackageEntry> RawViewWith(params RawPackageEntry[] extra) =>
    [
        RawPackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID)),
        RawPackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};"),
        .. extra,
    ];

    // The files of a folder, relative to it, with forward slashes.
    private static string[] FilesOf(string folder) =>
    [
        .. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/')),
    ];

    private static List<PackageEntry> ViewWith(params PackageEntry[] extra) => [.. TestPackage.View(VIEW_ID), .. extra];

    private static List<PackageEntry> ViewWithManifest(string manifest) =>
    [
        PackageEntry.Text("plugin.json", manifest),
        PackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};"),
    ];

    // Rewrites the size an entry announces, in its local header and in the central directory, keeping its data: the
    // archive then holds more than it says.
    private static void AnnounceSmallerSize(string package, string entryName, uint announcedSize)
    {
        var bytes = File.ReadAllBytes(package);
        var name = Encoding.UTF8.GetBytes(entryName);
        for (var offset = 0; offset < bytes.Length - 46; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) != 0x02014b50)
            {
                continue;
            }

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28));
            if (!bytes.AsSpan(offset + 46, nameLength).SequenceEqual(name))
            {
                continue;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 24), announcedSize);
            var localHeader = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 42));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(localHeader + 22), announcedSize);
        }

        File.WriteAllBytes(package, bytes);
    }

    private static List<PackageEntry> RefusedAfterExtraction(string scenario) => scenario switch
    {
        "native library" => ViewWith(new PackageEntry("Fixture.Native.dll", [0x4D, 0x5A, 0x90, 0x00, 0x03])),
        "invalid manifest" => ViewWithManifest("{ \"id\": \"fixture-view\" }"),
        "declared file missing" => [PackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID))],
        "contract not supported" => TestPackage.View(VIEW_ID, requires: """{ "contract": "plugin", "version": 99 }"""),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such scenario."),
    };

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public void Extract_ValidInterfacePlugin_IsAcceptedWithItsManifestAndContracts()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, TestPackage.View(VIEW_ID, "1.2.0"));

        extraction.Refusal.Should().BeNull();
        extraction.Manifest!.Id.Should().Be(VIEW_ID);
        extraction.Manifest.Version.Should().Be("1.2.0");
        extraction.Contracts.Should().Equal(new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public void Extract_ValidInterfacePlugin_ExtractsItsFilesWithTheirContent()
    {
        using var user = new UserPluginFolder();

        Extract(user, ViewWith(PackageEntry.Text("i18n/fr/messages.ftl", "view-title = Carnet")));

        var destination = DestinationOf(user);
        File.ReadAllText(Path.Combine(destination, "ui", TestPackage.UI_MODULE)).Should().Be("export default {};");
        File.ReadAllText(Path.Combine(destination, "i18n", "fr", "messages.ftl")).Should().Be("view-title = Carnet");
        File.Exists(Path.Combine(destination, "plugin.json")).Should().BeTrue();
    }

    [TestMethod]
    public void Extract_DataOfAnyKind_IsAcceptedAndExtracted()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(
            PackageEntry.Text("data/rates.csv", "EUR;1.00"),
            new PackageEntry("data/tools/convert.exe", [0x4D, 0x5A]),
            new PackageEntry("data/tiles/old-town.mbtiles", [1, 2, 3]),
            new PackageEntry("data/native.dll", [0x4D, 0x5A])));

        extraction.Refusal.Should().BeNull();
        Directory.EnumerateFiles(Path.Combine(DestinationOf(user), "data"), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(DestinationOf(user), file).Replace('\\', '/'))
            .Should().BeEquivalentTo("data/rates.csv", "data/tools/convert.exe", "data/tiles/old-town.mbtiles", "data/native.dll");
    }

    [TestMethod]
    public void Extract_FolderEntries_AreAccepted()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Folder("ui/"), PackageEntry.Folder("data/maps/"), PackageEntry.Folder("i18n/")));

        extraction.Refusal.Should().BeNull();
        Directory.Exists(Path.Combine(DestinationOf(user), "ui")).Should().BeTrue();
    }

    [TestMethod]
    public void Extract_Signature_IsAcceptedButNotExtracted()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Text("signature.json", """{ "key": "fixture-key", "value": "AAAA" }""")));

        extraction.Refusal.Should().BeNull();
        File.Exists(Path.Combine(DestinationOf(user), "signature.json")).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_PluginWithTheAssemblyOfATestPlugin_IsAccepted()
    {
        using var source = new PluginFolder();
        source.AddPlugin("fixture-good", "Good");
        using var user = new UserPluginFolder();

        var extraction = Extract(user, TestPackage.FromFolder(Path.Combine(source.Path, "fixture-good")));

        extraction.Refusal.Should().BeNull();
        extraction.Manifest!.DotnetAssembly.Should().Be("Voyager.PluginFixtures.Good.dll");
        File.Exists(Path.Combine(DestinationOf(user), "Voyager.PluginFixtures.Good.dll")).Should().BeTrue();
    }

    [TestMethod]
    public void Extract_DeprecatedContract_IsAcceptedAndShowsTheDeprecation()
    {
        using var user = new UserPluginFolder();
        var manifest = TestPackage.Manifest(VIEW_ID, provides: $$"""{ "contract": "{{ITINERARY_SOURCE}}", "version": 2 }""");

        var extraction = Extract(user, ViewWithManifest(manifest));

        extraction.Refusal.Should().BeNull();
        extraction.Contracts.Should().Contain(new ContractUse(ITINERARY_SOURCE, 2, ContractRole.Provided, ContractUseState.Deprecated));
    }

    [TestMethod]
    [DataRow("../x.txt", DisplayName = "zip slip")]
    [DataRow("data/../../x.txt", DisplayName = "zip slip from a folder")]
    [DataRow("/x.txt", DisplayName = "absolute path")]
    [DataRow("C:/x.txt", DisplayName = "drive")]
    [DataRow("data\\x.txt", DisplayName = "backslash")]
    [DataRow("data/a.txt:hidden", DisplayName = "alternate data stream")]
    [DataRow("con", DisplayName = "console at the root")]
    [DataRow("data/nul.txt", DisplayName = "nul with an extension")]
    [DataRow("data/com0", DisplayName = "com0")]
    [DataRow("ui/views/lpt9.js", DisplayName = "lpt9 in a sub-folder")]
    [DataRow("data/route.", DisplayName = "trailing dot")]
    [DataRow("data/route ", DisplayName = "trailing space")]
    [DataRow("setup.exe", DisplayName = "executable at the root")]
    [DataRow("ui/x.exe", DisplayName = "executable in ui")]
    [DataRow("bin/Fixture.Itinerary.dll", DisplayName = "other top folder")]
    [DataRow("readme.txt", DisplayName = "text at the root")]
    [DataRow("ui/LONGFI~1.JS", DisplayName = "8.3 short name")]
    [DataRow("data/PROGRA~2/rates.csv", DisplayName = "8.3 short name of a folder")]
    [DataRow("ui/com\u00B9.js", DisplayName = "com with a superscript one")]
    [DataRow("data/LPT\u00B3.txt", DisplayName = "lpt with a superscript three")]
    public void Extract_EntryWithAForbiddenName_IsRefusedAsForbiddenEntry(string entryName)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Text(entryName, "Traveler A")));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    [DataRow("other/", DisplayName = "other top folder")]
    [DataRow("../", DisplayName = "parent folder")]
    [DataRow("data/com1/", DisplayName = "device folder")]
    public void Extract_FolderWithAForbiddenName_IsRefusedAsForbiddenEntry(string folderName)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Folder(folderName)));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    [DataRow("ui/Map.js", "ui/map.js", DisplayName = "file names")]
    [DataRow("ui/Views/map.js", "ui/views/map.js", DisplayName = "folder names")]
    [DataRow("ui/map.js", "ui/map.js", DisplayName = "same name twice")]
    public void Extract_TwoEntriesThatAreOneFileOnWindows_AreRefusedAsForbiddenEntry(string first, string second)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Text(first, "export const a = 1;"), PackageEntry.Text(second, "export const b = 2;")));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    [DataRow("data/route", "data/route/day-1.txt", DisplayName = "file then a file inside it")]
    [DataRow("data/route/day-1.txt", "data/route", DisplayName = "file inside then the file")]
    [DataRow("data/Route", "data/route/day-1.txt", DisplayName = "names that differ by case")]
    public void Extract_FileAndFolderOfTheSameName_AreRefusedAsForbiddenEntry(string first, string second)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Text(first, "Day 1"), PackageEntry.Text(second, "Day 2")));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    public void Extract_FileAndFolderEntryOfTheSameName_AreRefusedAsForbiddenEntry()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(PackageEntry.Folder("data/route/"), PackageEntry.Text("data/route", "Day 1")));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    [DataRow(UNIX_SYMBOLIC_LINK, DisplayName = "Unix symbolic link")]
    [DataRow((int)FileAttributes.ReparsePoint, DisplayName = "Windows reparse point")]
    public void Extract_Link_IsRefusedAsForbiddenEntry(int externalAttributes)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(new PackageEntry("data/outside", Encoding.UTF8.GetBytes("../../../outside"), externalAttributes)));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 }, DisplayName = "a DOS header and nothing else")]
    [DataRow(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01 }, DisplayName = "a library of another system")]
    [DataRow(new byte[] { 0x20, 0x20, 0x20 }, DisplayName = "three spaces")]
    public void Extract_LibraryWithoutDotnetMetadata_IsRefusedAsForbiddenEntry(byte[] content)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(new PackageEntry("Fixture.Native.dll", content)));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    public void Extract_NativeLibraryOfTheSystem_IsRefusedAsForbiddenEntry()
    {
        var native = Path.Combine(Environment.SystemDirectory, "version.dll");
        Assert.IsTrue(File.Exists(native), "The test needs a native library of Windows.");
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(new PackageEntry("Fixture.Native.dll", File.ReadAllBytes(native))));

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
    }

    [TestMethod]
    public void Extract_NoManifest_IsRefusedAsManifestInvalid()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, [PackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};")]);

        extraction.Refusal.Should().Be(PackageRefusal.ManifestInvalid);
    }

    [TestMethod]
    [DataRow("not a manifest", DisplayName = "not JSON")]
    [DataRow("{}", DisplayName = "empty object")]
    [DataRow("""{ "id": "fixture-view", "version": "1.0.0", "provides": [], "requires": [{ "contract": "plugin", "version": 1 }] }""", DisplayName = "no part")]
    [DataRow("""{ "id": "service", "version": "1.0.0", "ui": { "module": "index.js" }, "provides": [], "requires": [{ "contract": "plugin", "version": 1 }] }""", DisplayName = "identifier of the service")]
    [DataRow("""{ "id": "con", "version": "1.0.0", "ui": { "module": "index.js" }, "provides": [], "requires": [{ "contract": "plugin", "version": 1 }] }""", DisplayName = "identifier of a device")]
    [DataRow("""{ "id": "Fixture View", "version": "1.0.0", "ui": { "module": "index.js" }, "provides": [], "requires": [{ "contract": "plugin", "version": 1 }] }""", DisplayName = "identifier badly formed")]
    [DataRow("""{ "id": "fixture-view", "id": "fixture-other", "version": "1.0.0", "ui": { "module": "index.js" }, "provides": [], "requires": [{ "contract": "plugin", "version": 1 }] }""", DisplayName = "key written twice")]
    [DataRow("""{ "id": "fixture-view", "version": "1.0.0", "ui": { "module": "index.js" }, "provides": [] }""", DisplayName = "no base contract")]
    public void Extract_InvalidManifest_IsRefusedAsManifestInvalid(string manifest)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWithManifest(manifest));

        extraction.Refusal.Should().Be(PackageRefusal.ManifestInvalid);
        extraction.Manifest.Should().BeNull();
    }

    [TestMethod]
    public void Extract_ManifestLargerThan64Kilobytes_IsRefusedAsManifestInvalid()
    {
        using var user = new UserPluginFolder();
        var manifest = TestPackage.Manifest(VIEW_ID) + new string(' ', 64 * 1024);

        var extraction = Extract(user, ViewWithManifest(manifest));

        extraction.Refusal.Should().Be(PackageRefusal.ManifestInvalid);
    }

    [TestMethod]
    public void Extract_DeclaredAssemblyMissing_IsRefusedAsDeclaredFileMissing()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWithManifest(TestPackage.Manifest(VIEW_ID, assembly: "Fixture.Itinerary.dll")));

        extraction.Refusal.Should().Be(PackageRefusal.DeclaredFileMissing);
    }

    [TestMethod]
    public void Extract_DeclaredInterfaceModuleMissing_IsRefusedAsDeclaredFileMissing()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, [PackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID)), PackageEntry.Text("ui/other.js", "export default {};")]);

        extraction.Refusal.Should().Be(PackageRefusal.DeclaredFileMissing);
    }

    [TestMethod]
    public void Extract_ContractInAVersionTheApplicationDoesNotKnow_IsRefusedWithTheContracts()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, TestPackage.View(VIEW_ID, requires: """{ "contract": "plugin", "version": 99 }"""));

        extraction.Refusal.Should().Be(PackageRefusal.ContractNotSupported);
        extraction.Manifest!.Id.Should().Be(VIEW_ID);
        extraction.Contracts.Should().Equal(new ContractUse(ContractVersions.PLUGIN, 99, ContractRole.Required, ContractUseState.Unknown));
    }

    [TestMethod]
    public void Extract_ObsoleteContract_IsRefusedAndListsEveryContract()
    {
        using var user = new UserPluginFolder();
        var manifest = TestPackage.Manifest(VIEW_ID, provides: $$"""{ "contract": "{{ITINERARY_SOURCE}}", "version": 1 }""");

        var extraction = Extract(user, ViewWithManifest(manifest));

        extraction.Refusal.Should().Be(PackageRefusal.ContractNotSupported);
        extraction.Contracts.Should().Equal(
            new ContractUse(ITINERARY_SOURCE, 1, ContractRole.Provided, ContractUseState.Obsolete),
            new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "empty file")]
    [DataRow(new byte[] { 0x13, 0x37, 0xC0, 0xFF, 0xEE, 0x42, 0x00, 0x7F }, DisplayName = "random bytes")]
    [DataRow(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 }, DisplayName = "the start of an archive")]
    public void Extract_FileThatIsNoArchive_IsRefusedAsUnreadable(byte[] content)
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.StageBytes(user, STAGED_NAME, content);

        var extraction = PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: true);

        extraction.Refusal.Should().Be(PackageRefusal.Unreadable);
    }

    [TestMethod]
    public void Extract_EntryThatHoldsMoreThanItAnnounces_IsRefusedAsUnreadable()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, ViewWith(PackageEntry.Text("data/rates.csv", string.Concat(Enumerable.Repeat("EUR;1.00\n", 500)))));
        AnnounceSmallerSize(package, "data/rates.csv", 10);

        var extraction = PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: true);

        extraction.Refusal.Should().Be(PackageRefusal.Unreadable);
    }

    [TestMethod]
    [DataRow("native library")]
    [DataRow("invalid manifest")]
    [DataRow("declared file missing")]
    [DataRow("contract not supported")]
    public void Extract_RefusedOnceExtracted_DeletesTheDestinationFolder(string scenario)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, RefusedAfterExtraction(scenario));

        extraction.Refusal.Should().NotBeNull();
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_EntryThatHoldsMoreThanItAnnounces_DeletesTheDestinationFolder()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, ViewWith(PackageEntry.Text("data/rates.csv", string.Concat(Enumerable.Repeat("EUR;1.00\n", 500)))));
        AnnounceSmallerSize(package, "data/rates.csv", 10);

        PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: true);

        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("１.０.０", DisplayName = "full-width digits")]
    [DataRow("١.٠.٠", DisplayName = "Arabic-Indic digits")]
    public void Extract_VersionWrittenWithDigitsOtherThanAscii_IsRefusedAsManifestInvalid(string version)
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWithManifest(TestPackage.Manifest(VIEW_ID, version)));

        extraction.Refusal.Should().Be(PackageRefusal.ManifestInvalid);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "accepted package")]
    [DataRow(false, DisplayName = "file that is no archive")]
    public void Extract_AnyPackage_GivesTheFingerprintAndTheSizeOfItsBytes(bool valid)
    {
        using var user = new UserPluginFolder();
        var package = valid
            ? TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID))
            : TestPackage.StageBytes(user, STAGED_NAME, [0x13, 0x37, 0xC0, 0xFF, 0xEE]);

        var extraction = PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: true);

        extraction.Sha256.Should().Be(TestPackage.Sha256Of(package));
        extraction.Size.Should().Be(new FileInfo(package).Length);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "installation")]
    [DataRow(false, DisplayName = "examination")]
    public void Extract_DamagedCentralDirectory_IsRefusedAsUnreadableWithoutException(bool everything)
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, ViewWith(PackageEntry.Text("data/rates.csv", "EUR;1.00")));
        TestPackage.DamageCentralDirectory(package);

        var extraction = PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything);

        extraction.Refusal.Should().Be(PackageRefusal.Unreadable);
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_TwoEntriesWhoseAnnouncedSizesAddUpBeyondTheLargestNumber_IsRefusedAsNotEnoughSpace()
    {
        using var user = new UserPluginFolder();
        // Each one is more than any disk holds; added without care, the two would go round to a negative size.
        const long announced = 0x6000_0000_0000_0000;

        var extraction = ExtractRaw(user, RawViewWith(
            new RawPackageEntry("data/tiles-a.mbtiles", [1, 2, 3], announced),
            new RawPackageEntry("data/tiles-b.mbtiles", [4, 5, 6], announced)));

        extraction.Refusal.Should().Be(PackageRefusal.NotEnoughSpace);
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_HandWrittenArchiveOfAValidPlugin_IsAccepted()
    {
        using var user = new UserPluginFolder();

        var extraction = ExtractRaw(user, RawViewWith(RawPackageEntry.Text("data/rates.csv", "EUR;1.00")));

        extraction.Refusal.Should().BeNull();
    }

    [TestMethod]
    [DataRow(true, DisplayName = "installation")]
    [DataRow(false, DisplayName = "examination")]
    public void Extract_EntryOfTheRightLengthWithAWrongCrc_IsRefusedAsUnreadable(bool everything)
    {
        using var user = new UserPluginFolder();

        var extraction = ExtractRaw(user, RawViewWith(new RawPackageEntry("data/rates.csv", "EUR;1.00"u8.ToArray(), WrongCrc: true)), everything);

        extraction.Refusal.Should().Be(PackageRefusal.Unreadable);
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_Examination_WritesOnlyTheFilesAtTheRoot()
    {
        using var source = new PluginFolder();
        source.AddPlugin("fixture-good", "Good");
        using var user = new UserPluginFolder();
        var entries = TestPackage.FromFolder(Path.Combine(source.Path, "fixture-good"));
        entries.Add(PackageEntry.Text("ui/index.js", "export default {};"));
        entries.Add(PackageEntry.Text("i18n/fr/messages.ftl", "view-title = Carnet"));
        entries.Add(PackageEntry.Text("data/rates.csv", "EUR;1.00"));

        var extraction = Extract(user, entries, everything: false);

        extraction.Refusal.Should().BeNull();
        FilesOf(DestinationOf(user)).Should().NotContain(file => file.Contains('/'));
        FilesOf(DestinationOf(user)).Should().Contain(["plugin.json", "Voyager.PluginFixtures.Good.dll"]);
    }

    [TestMethod]
    public void Extract_Examination_StillRefusesANativeLibraryAtTheRoot()
    {
        using var user = new UserPluginFolder();

        var extraction = Extract(user, ViewWith(new PackageEntry("Fixture.Native.dll", [0x4D, 0x5A, 0x90, 0x00, 0x03])), everything: false);

        extraction.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_Examination_StillRefusesADataEntryThatHoldsMoreThanItAnnounces()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, ViewWith(PackageEntry.Text("data/rates.csv", string.Concat(Enumerable.Repeat("EUR;1.00\n", 500)))));
        AnnounceSmallerSize(package, "data/rates.csv", 10);

        var extraction = PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: false);

        extraction.Refusal.Should().Be(PackageRefusal.Unreadable);
    }

    [TestMethod]
    public void Extract_CancelledBeforeReading_ThrowsAndLeavesNoDestinationFolder()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var extract = () => PluginPackageExtractor.Extract(package, DestinationOf(user), _contracts, everything: true, cancellation.Token);

        extract.Should().Throw<OperationCanceledException>();
        Directory.Exists(DestinationOf(user)).Should().BeFalse();
    }

    [TestMethod]
    public void Extract_ForbiddenEntry_WritesNothing()
    {
        using var user = new UserPluginFolder();

        Extract(user, ViewWith(PackageEntry.Text("../escaped.txt", "Traveler A")));

        Directory.Exists(DestinationOf(user)).Should().BeFalse();
        File.Exists(Path.Combine(user.Path, "escaped.txt")).Should().BeFalse();
    }

    #endregion Public Methods
}
