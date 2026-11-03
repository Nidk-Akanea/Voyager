using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class DisplayTextTests
{
    #region Properties

    // Built in code: an attribute keeps its texts in UTF-8, where half of a character cannot be written.
    public static IEnumerable<object[]> LoneSurrogates =>
    [
        ["Traveler\uD83CA", "TravelerA"],
        ["Traveler\uDF0DA", "TravelerA"],
        ["Traveler A\uD83C", "Traveler A"],
        ["\uDF0D\uD83CTraveler A", "Traveler A"],
    ];

    #endregion Properties

    #region Public Methods

    [TestMethod]
    [DataRow("travel-plugin.voyagerplugin")]
    [DataRow("Carnet d'été — Traveler A.voyagerplugin")]
    [DataRow("旅行 plugin.voyagerplugin")]
    public void Clean_PlainText_IsUnchanged(string text)
    {
        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(text);
    }

    [TestMethod]
    [DataRow("Traveler A\nplugin", "Traveler Aplugin", DisplayName = "line feed")]
    [DataRow("Traveler A\r\nplugin", "Traveler Aplugin", DisplayName = "carriage return and line feed")]
    [DataRow("Traveler A\tplugin", "Traveler Aplugin", DisplayName = "tab")]
    [DataRow("Traveler A\u0000plugin", "Traveler Aplugin", DisplayName = "null character")]
    [DataRow("Traveler A\u001Bplugin", "Traveler Aplugin", DisplayName = "escape")]
    [DataRow("Traveler A\u0085plugin", "Traveler Aplugin", DisplayName = "next line")]
    [DataRow("Traveler A\u2028plugin", "Traveler Aplugin", DisplayName = "line separator")]
    [DataRow("Traveler A\u2029plugin", "Traveler Aplugin", DisplayName = "paragraph separator")]
    public void Clean_ControlCharacters_AreRemoved(string text, string expected)
    {
        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("photos‮gpj.exe", "photosgpj.exe", DisplayName = "right-to-left override")]
    [DataRow("photos‭gpj.exe", "photosgpj.exe", DisplayName = "left-to-right override")]
    [DataRow("photos⁦gpj⁩.exe", "photosgpj.exe", DisplayName = "isolates")]
    [DataRow("photos⁧gpj⁨.exe", "photosgpj.exe", DisplayName = "right-to-left and first-strong isolates")]
    [DataRow("photos‎gpj‏.exe", "photosgpj.exe", DisplayName = "direction marks")]
    [DataRow("photos؜gpj.exe", "photosgpj.exe", DisplayName = "Arabic letter mark")]
    public void Clean_BidirectionalFormatting_IsRemoved(string text, string expected)
    {
        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("Traveler​A", "TravelerA", DisplayName = "zero-width space")]
    [DataRow("Traveler‌A", "TravelerA", DisplayName = "zero-width non-joiner")]
    [DataRow("Traveler‍A", "TravelerA", DisplayName = "zero-width joiner")]
    [DataRow("﻿Traveler A", "Traveler A", DisplayName = "byte order mark")]
    [DataRow("Traveler­A", "TravelerA", DisplayName = "soft hyphen")]
    public void Clean_InvisibleFormatCharacters_AreRemoved(string text, string expected)
    {
        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(expected);
    }

    [TestMethod]
    public void Clean_SpacesAround_AreTrimmed()
    {
        var cleaned = DisplayText.Clean("  \n Traveler A plugin \t ");

        cleaned.Should().Be("Traveler A plugin");
    }

    [TestMethod]
    public void Clean_TextOf150Characters_IsCutTo100WithAnEllipsis()
    {
        var text = new string('a', 150);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().HaveLength(DisplayText.MAXIMUM_LENGTH);
        cleaned.Should().Be(new string('a', 99) + "…");
    }

    [TestMethod]
    public void Clean_TextOf100Characters_IsNotCut()
    {
        var text = new string('a', 100);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(text);
    }

    [TestMethod]
    public void Clean_LongTextWhoseCutFallsOnASpace_HasNoSpaceBeforeTheEllipsis()
    {
        var text = new string('a', 98) + " " + new string('b', 50);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(new string('a', 98) + "…");
    }

    [TestMethod]
    public void Clean_LongTextOfControlCharactersAround_IsMeasuredOnceCleaned()
    {
        var text = new string('\n', 200) + new string('a', 100);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(new string('a', 100));
    }

    [TestMethod]
    public void Clean_CutInsideASurrogatePair_LeavesNoHalfCharacter()
    {
        var text = new string('a', 98) + "\U0001F30D" + new string('b', 50);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().NotContain("\uD83C");
        cleaned.Should().EndWith("…");
    }

    [TestMethod]
    [DynamicData(nameof(LoneSurrogates))]
    public void Clean_LoneSurrogate_IsRemoved(string text, string expected)
    {
        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(expected);
    }

    [TestMethod]
    public void Clean_CharacterOutsideTheBasicPlane_IsKept()
    {
        var cleaned = DisplayText.Clean("Traveler A \U0001F30D");

        cleaned.Should().Be("Traveler A \U0001F30D");
    }

    [TestMethod]
    public void Clean_LongTextWhoseCutFallsJustAfterASurrogatePair_KeepsThePairWhole()
    {
        var text = new string('a', 97) + "\U0001F30D" + new string('b', 50);

        var cleaned = DisplayText.Clean(text);

        cleaned.Should().Be(new string('a', 97) + "\U0001F30D…");
    }

    [TestMethod]
    public void Clean_EmptyText_IsEmpty()
    {
        var cleaned = DisplayText.Clean(string.Empty);

        cleaned.Should().BeEmpty();
    }

    #endregion Public Methods
}
