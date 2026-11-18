namespace Voyager.Core.Shared;

/// <summary>
/// An expected business failure, as a value: no exception, since the caller is bound to meet it (docs/conventions/csharp.md).
/// </summary>
/// <param name="Kind">The family of the failure.</param>
/// <param name="Code">
/// A stable reason in kebab-case, such as <c>name-required</c>: never a text, since the interface translates it.
/// </param>
/// <param name="Field">The field the failure is about, for an invalid request; null otherwise.</param>
public sealed record Error(ErrorKind Kind, string Code, string? Field = null);
