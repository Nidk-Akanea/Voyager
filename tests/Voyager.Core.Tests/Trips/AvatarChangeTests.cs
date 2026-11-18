using AwesomeAssertions;

using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class AvatarChangeTests
{
    #region Public Methods

    [TestMethod]
    public void Default_NoChangeAsked_KeepsTheAvatar()
    {
        AvatarChange change = default;

        change.Kind.Should().Be(AvatarChangeKind.Keep);
    }

    [TestMethod]
    public void Keep_Always_IsKeep()
    {
        AvatarChange.Keep.Kind.Should().Be(AvatarChangeKind.Keep);
    }

    [TestMethod]
    public void Remove_Always_IsRemoveWithoutContent()
    {
        var change = AvatarChange.Remove;

        change.Kind.Should().Be(AvatarChangeKind.Remove);
        change.Content.Length.Should().Be(0);
    }

    [TestMethod]
    public void Replace_WithContent_IsReplaceCarryingTheContent()
    {
        var content = TestImages.Png();

        var change = AvatarChange.Replace(content);

        change.Kind.Should().Be(AvatarChangeKind.Replace);
        change.Content.ToArray().Should().Equal(content);
    }

    #endregion Public Methods
}
