using NodaTime;

namespace Voyager.Sdk.Diagnostics;

/// <summary>
/// The value of a <see cref="DiagnosticFact"/>: a text, a number or an instant. Create one with
/// <see cref="FromText"/>, <see cref="FromNumber"/> or <see cref="FromInstant"/>.
/// </summary>
public sealed record DiagnosticFactValue
{
    #region Constructors

    private DiagnosticFactValue(DiagnosticFactKind kind, string text, double number, Instant instant)
    {
        Kind = kind;
        Text = text;
        Number = number;
        Instant = instant;
    }

    #endregion Constructors

    #region Properties

    /// <summary>What this value holds.</summary>
    public DiagnosticFactKind Kind { get; }

    /// <summary>The text, when <see cref="Kind"/> is <see cref="DiagnosticFactKind.Text"/>; empty otherwise.</summary>
    public string Text { get; }

    /// <summary>The number, when <see cref="Kind"/> is <see cref="DiagnosticFactKind.Number"/>; zero otherwise.</summary>
    public double Number { get; }

    /// <summary>The instant, when <see cref="Kind"/> is <see cref="DiagnosticFactKind.Instant"/>; the epoch otherwise.</summary>
    public Instant Instant { get; }

    #endregion Properties

    #region Public Methods

    /// <summary>Creates a text value.</summary>
    /// <param name="text">
    /// The text, at most 1000 characters; it must not hold personal data (account name, machine name, paths).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static DiagnosticFactValue FromText(string text) =>
        new(DiagnosticFactKind.Text, text ?? throw new ArgumentNullException(nameof(text)), 0, default);

    /// <summary>Creates a number value.</summary>
    /// <param name="number">The number; it must be finite (not NaN, not infinity), or the source is reported as invalid.</param>
    public static DiagnosticFactValue FromNumber(double number) =>
        new(DiagnosticFactKind.Number, string.Empty, number, default);

    /// <summary>Creates an instant value.</summary>
    /// <param name="instant">
    /// The instant, in UTC; the interface shows it in the user's time zone. It must be between the years
    /// 1 and 9999, or the source is reported as invalid.
    /// </param>
    public static DiagnosticFactValue FromInstant(Instant instant) =>
        new(DiagnosticFactKind.Instant, string.Empty, 0, instant);

    #endregion Public Methods
}
