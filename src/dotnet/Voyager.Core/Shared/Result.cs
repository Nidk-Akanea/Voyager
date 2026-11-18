namespace Voyager.Core.Shared;

/// <summary>
/// Makes the <see cref="Result{T}"/> of the operations of the core.
/// </summary>
public static class Result
{
    #region Public Methods

    /// <summary>A failure.</summary>
    /// <typeparam name="T">The type of the value a success would have had.</typeparam>
    /// <param name="error">Why the operation failed.</param>
    /// <returns>The failed result.</returns>
    public static Result<T> Failure<T>(Error error) => new(default, error);

    /// <summary>A success of an operation that has nothing to give back.</summary>
    /// <returns>The successful result.</returns>
    public static Result<Unit> Success() => new(Unit.Value, null);

    /// <summary>A success.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">What the operation gives back.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success<T>(T value) => new(value, null);

    #endregion Public Methods
}
