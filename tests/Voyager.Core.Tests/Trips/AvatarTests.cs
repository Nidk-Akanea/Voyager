using AwesomeAssertions;

using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class AvatarTests
{
    #region Public Methods

    [TestMethod]
    public void Create_PngContent_IsRecognisedAsPng()
    {
        var result = Avatar.Create(TestImages.Png());

        result.ShouldSucceed().Format.Should().Be(AvatarFormat.Png);
    }

    [TestMethod]
    public void Create_JpegContent_IsRecognisedAsJpeg()
    {
        var result = Avatar.Create(TestImages.Jpeg());

        result.ShouldSucceed().Format.Should().Be(AvatarFormat.Jpeg);
    }

    [TestMethod]
    public void Create_WebPContent_IsRecognisedAsWebP()
    {
        var result = Avatar.Create(TestImages.WebP());

        result.ShouldSucceed().Format.Should().Be(AvatarFormat.WebP);
    }

    [TestMethod]
    public void Create_PngOfOneMegabyte_IsAccepted()
    {
        var result = Avatar.Create(TestImages.Png(1024 * 1024));

        result.ShouldSucceed().Content.Length.Should().Be(1024 * 1024);
    }

    [TestMethod]
    public void Create_PngOfExactlyTwoMegabytes_IsAccepted()
    {
        var result = Avatar.Create(TestImages.Png(2 * 1024 * 1024));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_PngOfTwoMegabytesPlusOneByte_IsRefusedAsTooLarge()
    {
        var result = Avatar.Create(TestImages.Png((2 * 1024 * 1024) + 1));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
    }

    [TestMethod]
    public void Create_ImageOfThreeMegabytes_IsRefusedAsTooLarge()
    {
        var result = Avatar.Create(TestImages.Jpeg(3 * 1024 * 1024));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
    }

    [TestMethod]
    public void Create_TextFile_IsRefusedAsNotAnImage()
    {
        var result = Avatar.Create(TestImages.Text());

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_SvgTextNamedLikeAnImage_IsRefusedAsNotAnImage()
    {
        // The format is read from the content: a text that claims to be a picture is still a text.
        var result = Avatar.Create(TestImages.Text("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_EmptyContent_IsRefusedAsNotAnImage()
    {
        var result = Avatar.Create(ReadOnlyMemory<byte>.Empty);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_RiffContainerThatIsNotWebP_IsRefusedAsNotAnImage()
    {
        var result = Avatar.Create(TestImages.WebP(type: "WAVE"));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_TruncatedRiffHeader_IsRefusedAsNotAnImage()
    {
        var result = Avatar.Create(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00 });

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
    }

    [TestMethod]
    public void Create_SourceArrayChangedAfterwards_KeepsItsOwnCopy()
    {
        var source = TestImages.Png();
        var avatar = Avatar.Create(source).ShouldSucceed();

        source[20] = 0x7F;

        avatar.Content.ToArray()[20].Should().Be(0);
    }

    [TestMethod]
    public void HasSameContentAs_SameBytesAndFormat_IsTrue()
    {
        var first = Avatar.Create(TestImages.Png()).ShouldSucceed();
        var second = Avatar.Create(TestImages.Png()).ShouldSucceed();

        first.HasSameContentAs(second).Should().BeTrue();
    }

    [TestMethod]
    public void HasSameContentAs_OtherBytes_IsFalse()
    {
        var first = Avatar.Create(TestImages.Png(64)).ShouldSucceed();
        var second = Avatar.Create(TestImages.Png(65)).ShouldSucceed();

        first.HasSameContentAs(second).Should().BeFalse();
    }

    [TestMethod]
    public void HasSameContentAs_OtherFormat_IsFalse()
    {
        var png = Avatar.Create(TestImages.Png()).ShouldSucceed();
        var jpeg = Avatar.Create(TestImages.Jpeg()).ShouldSucceed();

        png.HasSameContentAs(jpeg).Should().BeFalse();
    }

    [TestMethod]
    public void Restore_StoredContent_KeepsContentAndFormat()
    {
        var content = TestImages.WebP();

        var avatar = Avatar.Restore(content, AvatarFormat.WebP);

        avatar.Format.Should().Be(AvatarFormat.WebP);
        avatar.Content.ToArray().Should().Equal(content);
    }

    #endregion Public Methods
}
