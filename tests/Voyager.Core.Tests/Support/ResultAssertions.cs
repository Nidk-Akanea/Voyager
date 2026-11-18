using AwesomeAssertions;

using Voyager.Core.Shared;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// Reads a <see cref="Result{T}" /> in a test: the value of a success, or the error that was expected.
/// </summary>
internal static class ResultAssertions
{
    #region Public Methods

    public static void ShouldFailWith<T>(this Result<T> result, Error expected)
    {
        result.IsFailure.Should().BeTrue("the result was expected to fail with {0}", expected);
        result.Error.Should().Be(expected);
    }

    public static T ShouldSucceed<T>(this Result<T> result)
    {
        var failure = result.IsFailure ? result.Error : null;
        result.IsSuccess.Should().BeTrue("the result failed with {0}", failure);
        return result.Value;
    }

    #endregion Public Methods
}
