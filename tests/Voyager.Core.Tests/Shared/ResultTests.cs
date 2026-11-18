using AwesomeAssertions;

using Voyager.Core.Shared;

namespace Voyager.Core.Tests.Shared;

[TestClass]
public sealed class ResultTests
{
    #region Public Methods

    [TestMethod]
    public void Error_FailedResult_IsTheErrorGiven()
    {
        var error = new Error(ErrorKind.NotFound, "something-not-found");

        var result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [TestMethod]
    public void Error_SuccessfulResult_Throws()
    {
        var result = Result.Success("value");

        var read = () => result.Error;

        read.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Success_WithoutValue_CarriesTheUnit()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [TestMethod]
    public void Success_WithValue_CarriesTheValue()
    {
        var result = Result.Success("Traveler A");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("Traveler A");
    }

    [TestMethod]
    public void Value_FailedResult_Throws()
    {
        var result = Result.Failure<string>(new Error(ErrorKind.Invalid, "name-required", "name"));

        var read = () => result.Value;

        read.Should().Throw<InvalidOperationException>();
    }

    #endregion Public Methods
}
