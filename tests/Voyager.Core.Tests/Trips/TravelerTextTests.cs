using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

/// <summary>
/// What cleaning and limiting a text does to the invisible and the oversized: no name that looks empty, no text that weighs
/// more than a few kilobytes.
/// </summary>
[TestClass]
public sealed class TravelerTextTests
{
    #region Fields

    // A family of four, one emoji made of eleven code units.
    private const string FAMILY = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

    #endregion Fields

    #region Private Methods

    private static Result<Traveler> Create(string? name = "Traveler A", string? description = null) =>
        Traveler.Create(
            TravelerId.New(),
            UserId.New(),
            isProfile: false,
            TestDetails.Of(name: name, description: description),
            AvatarChange.Keep,
            TripsWorld.Today);

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow("\u200B", DisplayName = "Zero-width space")]
    [DataRow("\u00AD", DisplayName = "Soft hyphen")]
    [DataRow("\uFEFF", DisplayName = "Byte order mark")]
    [DataRow("\u2060", DisplayName = "Word joiner")]
    [DataRow("\u2028", DisplayName = "Line separator")]
    [DataRow("\u2029", DisplayName = "Paragraph separator")]
    [DataRow(" \u200B \u2060 ", DisplayName = "Invisible characters and spaces")]
    public void Create_NameThatIsOnlyInvisible_IsRequired(string name)
    {
        var result = Create(name);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
    }

    [TestMethod]
    public void Create_InvisibleCharacterInsideAName_IsTakenOut()
    {
        var result = Create("Traveler\u200B A\u00AD");

        result.ShouldSucceed().Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public void Create_EmojiMadeOfJoinedParts_KeepsItsJoiners()
    {
        var result = Create(FAMILY);

        result.ShouldSucceed().Name.Should().Be(FAMILY);
    }

    [TestMethod]
    public void Create_NonJoinerOfAScriptThatNeedsIt_StaysInTheName()
    {
        const string name = "\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645";

        var result = Create(name);

        result.ShouldSucceed().Name.Should().Be(name);
    }

    [TestMethod]
    public void Create_HalfOfASurrogatePairAlone_IsTakenOut()
    {
        var result = Create("Traveler\uD800 A\uDC00");

        result.ShouldSucceed().Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public void Create_CharacterOutsideTheBasicPlane_StaysWhole()
    {
        var result = Create("Traveler \U0001F600");

        result.ShouldSucceed().Name.Should().Be("Traveler \U0001F600");
    }

    [TestMethod]
    public void Create_HundredEmojisOfManyCodeUnits_IsNotTooLong()
    {
        var name = string.Concat(Enumerable.Repeat(FAMILY, Traveler.MAXIMUM_NAME_LENGTH));

        var result = Create(name);

        result.ShouldSucceed().Name.Should().Be(name);
    }

    [TestMethod]
    public void Create_LetterWithThousandsOfCombiningMarks_IsTooLong()
    {
        var name = "a" + new string('\u0301', 10_000);

        var result = Create(name);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public void Create_DescriptionWithThousandsOfCombiningMarks_IsTooLong()
    {
        var description = "a" + new string('\u0301', 10_000);

        var result = Create(description: description);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.DESCRIPTION_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public void Create_LineSeparatorInADescription_IsNotALineBreak()
    {
        var result = Create(description: "First\u2028Second\nThird");

        result.ShouldSucceed().Description.Should().Be("FirstSecond\nThird");
    }

    #endregion Public Methods
}
