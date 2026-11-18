using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class TravelerTests
{
    #region Fields

    private static readonly LocalDate _today = TripsWorld.Today;

    #endregion Fields

    #region Public Methods

    #region Creation

    [TestMethod]
    public void Create_ValidDetails_KeepsEveryField()
    {
        var id = TravelerId.New();
        var owner = UserId.New();
        var details = TestDetails.Of("Traveler A", "TA", new LocalDate(1990, 3, 15), "Likes long walks.");

        var traveler = Traveler.Create(id, owner, isProfile: false, details, AvatarChange.Keep, _today).ShouldSucceed();

        traveler.Id.Should().Be(id);
        traveler.OwnerId.Should().Be(owner);
        traveler.IsProfile.Should().BeFalse();
        traveler.Name.Should().Be("Traveler A");
        traveler.Nickname.Should().Be("TA");
        traveler.BirthDate.Should().Be(new LocalDate(1990, 3, 15));
        traveler.Description.Should().Be("Likes long walks.");
        traveler.Avatar.Should().BeNull();
    }

    [TestMethod]
    public void Create_AsProfile_IsMarkedAsTheProfile()
    {
        var traveler = Create(TestDetails.Of(), isProfile: true).ShouldSucceed();

        traveler.IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public void Create_OnlyAName_SucceedsWithTheRestMissing()
    {
        var traveler = Create(TestDetails.Of("Child C")).ShouldSucceed();

        traveler.Name.Should().Be("Child C");
        traveler.Nickname.Should().BeNull();
        traveler.BirthDate.Should().BeNull();
        traveler.Avatar.Should().BeNull();
        traveler.Description.Should().BeNull();
    }

    #endregion Creation

    #region Name

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t \n ")]
    public void Create_NameMissingOrBlank_IsRefusedAsRequired(string? name)
    {
        var result = Create(TestDetails.Of(name));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
    }

    [TestMethod]
    public void Create_NameOf100Characters_IsAccepted()
    {
        var result = Create(TestDetails.Of(new string('a', 100)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NameOf101Characters_IsRefusedAsTooLong()
    {
        var result = Create(TestDetails.Of(new string('a', 101)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public void Create_NameOf100LettersWithCombiningAccents_IsAccepted()
    {
        // "e" followed by a combining acute accent is two code points but one letter for its reader.
        var name = string.Concat(Enumerable.Repeat("é", 100));

        var result = Create(TestDetails.Of(name));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NameOf101LettersWithCombiningAccents_IsRefusedAsTooLong()
    {
        var name = string.Concat(Enumerable.Repeat("é", 101));

        var result = Create(TestDetails.Of(name));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public void Create_NameOf100EmojisMadeOfTwoUtf16Units_IsAccepted()
    {
        var name = string.Concat(Enumerable.Repeat("\U0001F600", 100));

        var result = Create(TestDetails.Of(name));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NameWithSpacesAround_IsTrimmed()
    {
        var traveler = Create(TestDetails.Of("   Traveler A \t")).ShouldSucceed();

        traveler.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public void Create_NameOf100CharactersPaddedWithSpaces_IsAccepted()
    {
        // The length is the one of the cleaned text.
        var result = Create(TestDetails.Of("  " + new string('a', 100) + "  "));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NameWithLineBreakAndControlCharacters_HasThemRemoved()
    {
        var traveler = Create(TestDetails.Of("Trav\neler\r A\t\0\u007F")).ShouldSucceed();

        traveler.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    [DataRow("‮", DisplayName = "right-to-left override U+202E")]
    [DataRow("‪", DisplayName = "left-to-right embedding U+202A")]
    [DataRow("‫", DisplayName = "right-to-left embedding U+202B")]
    [DataRow("‬", DisplayName = "pop directional formatting U+202C")]
    [DataRow("‭", DisplayName = "left-to-right override U+202D")]
    [DataRow("‎", DisplayName = "left-to-right mark U+200E")]
    [DataRow("‏", DisplayName = "right-to-left mark U+200F")]
    [DataRow("؜", DisplayName = "arabic letter mark U+061C")]
    [DataRow("⁦", DisplayName = "left-to-right isolate U+2066")]
    [DataRow("⁧", DisplayName = "right-to-left isolate U+2067")]
    [DataRow("⁨", DisplayName = "first strong isolate U+2068")]
    [DataRow("⁩", DisplayName = "pop directional isolate U+2069")]
    public void Create_NameWithBidirectionalCharacter_HasItRemoved(string bidirectional)
    {
        var traveler = Create(TestDetails.Of($"Trav{bidirectional}eler A")).ShouldSucceed();

        traveler.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public void Create_NameMadeOnlyOfBidirectionalCharacters_IsRefusedAsRequired()
    {
        var result = Create(TestDetails.Of("‮‏⁦"));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
    }

    [TestMethod]
    public void Create_TwoTravelersWithTheSameName_AreBothAccepted()
    {
        var first = Create(TestDetails.Of("Traveler A"));
        var second = Create(TestDetails.Of("Traveler A"));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
    }

    #endregion Name

    #region Nickname

    [TestMethod]
    public void Create_NicknameOf50Characters_IsAccepted()
    {
        var result = Create(TestDetails.Of(nickname: new string('t', 50)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NicknameOf51Characters_IsRefusedAsTooLong()
    {
        var result = Create(TestDetails.Of(nickname: new string('t', 51)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NICKNAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("‮")]
    public void Create_NicknameMissingOrBlank_MeansNoNickname(string? nickname)
    {
        var traveler = Create(TestDetails.Of(nickname: nickname)).ShouldSucceed();

        traveler.Nickname.Should().BeNull();
    }

    [TestMethod]
    public void Create_NicknameWithSpacesAndBidirectionalCharacter_IsCleaned()
    {
        var traveler = Create(TestDetails.Of(nickname: "  T‮A\n ")).ShouldSucceed();

        traveler.Nickname.Should().Be("TA");
    }

    [TestMethod]
    public void DisplayName_WithNickname_IsTheNickname()
    {
        var traveler = Create(TestDetails.Of("Traveler A", "TA")).ShouldSucceed();

        traveler.DisplayName.Should().Be("TA");
    }

    [TestMethod]
    public void DisplayName_WithoutNickname_IsTheName()
    {
        var traveler = Create(TestDetails.Of("Child C")).ShouldSucceed();

        traveler.DisplayName.Should().Be("Child C");
    }

    #endregion Nickname

    #region Description

    [TestMethod]
    public void Create_DescriptionOf500Characters_IsAccepted()
    {
        var result = Create(TestDetails.Of(description: new string('d', 500)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_DescriptionOf501Characters_IsRefusedAsTooLong()
    {
        var result = Create(TestDetails.Of(description: new string('d', 501)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.DESCRIPTION_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \n ")]
    public void Create_DescriptionMissingOrBlank_MeansNoDescription(string? description)
    {
        var traveler = Create(TestDetails.Of(description: description)).ShouldSucceed();

        traveler.Description.Should().BeNull();
    }

    [TestMethod]
    public void Create_DescriptionWithLineBreaks_KeepsThem()
    {
        var traveler = Create(TestDetails.Of(description: "First line\nSecond line")).ShouldSucceed();

        traveler.Description.Should().Be("First line\nSecond line");
    }

    [TestMethod]
    public void Create_DescriptionWithWindowsLineBreaks_KeepsASingleLineFeed()
    {
        var traveler = Create(TestDetails.Of(description: "First line\r\nSecond line")).ShouldSucceed();

        traveler.Description.Should().Be("First line\nSecond line");
    }

    [TestMethod]
    public void Create_DescriptionWithOtherControlAndBidirectionalCharacters_HasThemRemoved()
    {
        var traveler = Create(TestDetails.Of(description: "Tab\there\0 and ‮direction‏")).ShouldSucceed();

        traveler.Description.Should().Be("Tabhere and direction");
    }

    [TestMethod]
    public void Create_DescriptionWithBlankLinesAround_IsTrimmed()
    {
        var traveler = Create(TestDetails.Of(description: "\n\n  A short note.  \n")).ShouldSucceed();

        traveler.Description.Should().Be("A short note.");
    }

    [TestMethod]
    public void Create_NameWithLineBreak_DoesNotKeepIt()
    {
        // Only the description accepts line breaks.
        var traveler = Create(TestDetails.Of("Traveler\nA", nickname: "T\nA")).ShouldSucceed();

        traveler.Name.Should().Be("TravelerA");
        traveler.Nickname.Should().Be("TA");
    }

    #endregion Description

    #region Birth date

    [TestMethod]
    public void Create_BirthDateInThePast_IsAccepted()
    {
        var traveler = Create(TestDetails.Of(birthDate: new LocalDate(2018, 7, 2))).ShouldSucceed();

        traveler.BirthDate.Should().Be(new LocalDate(2018, 7, 2));
    }

    [TestMethod]
    public void Create_BirthDateToday_IsAccepted()
    {
        var result = Create(TestDetails.Of(birthDate: _today));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_BirthDateTomorrow_IsAcceptedForTimeZonesAheadOfUtc()
    {
        var result = Create(TestDetails.Of(birthDate: _today.PlusDays(1)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_BirthDateTheDayAfterTomorrow_IsRefusedAsInTheFuture()
    {
        var result = Create(TestDetails.Of(birthDate: _today.PlusDays(2)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.BIRTH_DATE_FIELD, TripsErrors.IN_THE_FUTURE));
    }

    [TestMethod]
    public void Create_BirthDateAtTheEndOfAMonthAndYear_FollowsTheCalendarForTomorrow()
    {
        var newYearsEve = new LocalDate(2026, 12, 31);

        var accepted = Create(TestDetails.Of(birthDate: new LocalDate(2027, 1, 1)), today: newYearsEve);
        var refused = Create(TestDetails.Of(birthDate: new LocalDate(2027, 1, 2)), today: newYearsEve);

        accepted.IsSuccess.Should().BeTrue();
        refused.ShouldFailWith(TripsErrors.Invalid(TripsErrors.BIRTH_DATE_FIELD, TripsErrors.IN_THE_FUTURE));
    }

    #endregion Birth date

    #region Avatar

    [TestMethod]
    public void Create_AvatarReplaceWithPng_KeepsTheAvatar()
    {
        var png = TestImages.Png(1024 * 1024);

        var traveler = Create(TestDetails.Of(), AvatarChange.Replace(png)).ShouldSucceed();

        traveler.Avatar.Should().NotBeNull();
        traveler.Avatar!.Format.Should().Be(AvatarFormat.Png);
        traveler.Avatar.Content.Length.Should().Be(png.Length);
    }

    [TestMethod]
    public void Create_AvatarReplaceWithJpegOrWebP_KeepsTheRightFormat()
    {
        var jpeg = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.Jpeg())).ShouldSucceed();
        var webP = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.WebP())).ShouldSucceed();

        jpeg.Avatar!.Format.Should().Be(AvatarFormat.Jpeg);
        webP.Avatar!.Format.Should().Be(AvatarFormat.WebP);
    }

    [TestMethod]
    public void Create_AvatarReplaceWithTextFile_IsRefusedAsNotAnImage()
    {
        var result = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.Text()));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_AvatarReplaceWithImageOfThreeMegabytes_IsRefusedAsTooLarge()
    {
        var result = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.Png(3 * 1024 * 1024)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
    }

    [TestMethod]
    public void Create_AvatarReplaceWithTwoMegabytesPlusOneByte_IsRefusedAsTooLarge()
    {
        var result = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.Png((2 * 1024 * 1024) + 1)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
    }

    [TestMethod]
    public void Create_AvatarReplaceWithTwoMegabytes_IsAccepted()
    {
        var result = Create(TestDetails.Of(), AvatarChange.Replace(TestImages.Png(2 * 1024 * 1024)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_AvatarRemove_MeansNoAvatar()
    {
        var traveler = Create(TestDetails.Of(), AvatarChange.Remove).ShouldSucceed();

        traveler.Avatar.Should().BeNull();
    }

    #endregion Avatar

    #region Update

    [TestMethod]
    public void Update_NewDetails_ChangesThemAndKeepsIdentityOwnerAndProfileMark()
    {
        var traveler = new TravelerBuilder().WithName("Traveler A").AsProfile().Build();

        var updated = traveler.Update(TestDetails.Of("Traveler B", "TB"), AvatarChange.Keep, _today).ShouldSucceed();

        updated.Id.Should().Be(traveler.Id);
        updated.OwnerId.Should().Be(traveler.OwnerId);
        updated.IsProfile.Should().BeTrue();
        updated.Name.Should().Be("Traveler B");
        updated.Nickname.Should().Be("TB");
    }

    [TestMethod]
    public void Update_NoNickname_ClearsTheNickname()
    {
        var traveler = new TravelerBuilder().WithNickname("TA").Build();

        var updated = traveler.Update(TestDetails.Of("Traveler A"), AvatarChange.Keep, _today).ShouldSucceed();

        updated.Nickname.Should().BeNull();
    }

    [TestMethod]
    public void Update_AvatarKeep_KeepsTheCurrentAvatar()
    {
        var traveler = new TravelerBuilder().WithAvatar(TestImages.Png()).Build();

        var updated = traveler.Update(TestDetails.Of(), AvatarChange.Keep, _today).ShouldSucceed();

        updated.Avatar.Should().BeSameAs(traveler.Avatar);
    }

    [TestMethod]
    public void Update_AvatarRemove_RemovesTheAvatar()
    {
        var traveler = new TravelerBuilder().WithAvatar(TestImages.Png()).Build();

        var updated = traveler.Update(TestDetails.Of(), AvatarChange.Remove, _today).ShouldSucceed();

        updated.Avatar.Should().BeNull();
    }

    [TestMethod]
    public void Update_AvatarReplace_ReplacesTheAvatar()
    {
        var traveler = new TravelerBuilder().WithAvatar(TestImages.Png()).Build();

        var updated = traveler.Update(TestDetails.Of(), AvatarChange.Replace(TestImages.Jpeg()), _today).ShouldSucceed();

        updated.Avatar!.Format.Should().Be(AvatarFormat.Jpeg);
    }

    [TestMethod]
    public void Update_AvatarReplaceWithTextFile_IsRefusedAndTheTravelerKeepsItsAvatar()
    {
        var traveler = new TravelerBuilder().WithAvatar(TestImages.Png()).Build();

        var result = traveler.Update(TestDetails.Of(), AvatarChange.Replace(TestImages.Text()), _today);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
        traveler.Avatar.Should().NotBeNull();
        traveler.Avatar!.Format.Should().Be(AvatarFormat.Png);
    }

    [TestMethod]
    public void Update_BlankName_IsRefusedAndTheTravelerIsUnchanged()
    {
        var traveler = new TravelerBuilder().WithName("Traveler A").Build();

        var result = traveler.Update(TestDetails.Of("  "), AvatarChange.Keep, _today);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
        traveler.Name.Should().Be("Traveler A");
    }

    #endregion Update

    #region Same content

    [TestMethod]
    public void HasSameContentAs_SameEverything_IsTrue()
    {
        var owner = UserId.New();
        var first = Build(owner);
        var second = Build(owner);

        first.HasSameContentAs(second).Should().BeTrue();
    }

    [TestMethod]
    public void HasSameContentAs_OtherName_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, name: "Traveler B")).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherNickname_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, nickname: "Other")).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherBirthDate_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, birthDate: new LocalDate(1990, 3, 16))).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherDescription_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, description: "Other")).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherAvatar_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, avatar: TestImages.Png(65))).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_AvatarOnOneSideOnly_IsFalse()
    {
        var owner = UserId.New();
        var withAvatar = Build(owner);
        var without = Build(owner, withAvatar: false);

        withAvatar.HasSameContentAs(without).Should().BeFalse();
        without.HasSameContentAs(withAvatar).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherOwner_IsFalse()
    {
        Build(UserId.New()).HasSameContentAs(Build(UserId.New())).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_ProfileAgainstOrdinaryTraveler_IsFalse()
    {
        var owner = UserId.New();

        Build(owner).HasSameContentAs(Build(owner, isProfile: true)).Should().BeFalse();
    }

    #endregion Same content

    #endregion Public Methods

    #region Private Methods

    private static Result<Traveler> Create(
        TravelerDetails details,
        AvatarChange avatar = default,
        bool isProfile = false,
        LocalDate? today = null) =>
        Traveler.Create(TravelerId.New(), UserId.New(), isProfile, details, avatar, today ?? _today);

    // A traveler with an avatar by default, so that a test only names what differs.
    private static Traveler Build(
        UserId owner,
        string name = "Traveler A",
        string? nickname = "TA",
        LocalDate? birthDate = null,
        string? description = "Likes long walks.",
        byte[]? avatar = null,
        bool isProfile = false,
        bool withAvatar = true)
    {
        var builder = new TravelerBuilder()
            .WithOwner(owner)
            .WithName(name)
            .WithBirthDate(birthDate ?? new LocalDate(1990, 3, 15));
        if (withAvatar)
        {
            builder.WithAvatar(avatar ?? TestImages.Png(64));
        }

        if (nickname is not null)
        {
            builder.WithNickname(nickname);
        }

        if (description is not null)
        {
            builder.WithDescription(description);
        }

        return isProfile ? builder.AsProfile().Build() : builder.Build();
    }

    #endregion Private Methods
}
