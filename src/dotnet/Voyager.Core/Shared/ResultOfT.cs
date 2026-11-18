namespace Voyager.Core.Shared;

/// <summary>
/// The outcome of an operation that can fail for a reason the caller expects: a value, or an <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The type of the value of a success.</typeparam>
/// <remarks>
/// A result is made with <see cref="Result"/>. The default value of the type, which no one makes on purpose, counts as a
/// success with no value.
/// </remarks>
public readonly struct Result<T>
{
    #region Fields

    private readonly T? _value;
    private readonly Error? _error;

    #endregion Fields

    #region Internal Constructors

    internal Result(T? value, Error? error)
    {
        _value = value;
        _error = error;
    }

    #endregion Internal Constructors

    #region Properties

    /// <summary>The failure; reading it from a success is a bug.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => _error is not null;

    /// <summary>Whether the operation worked.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>The value of a success; reading it from a failure is a bug.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public T Value => _error is null ? _value! : throw new InvalidOperationException("A failed result has no value.");

    #endregion Properties
}
