namespace Voyager.Sdk.Diagnostics;

/// <summary>
/// One piece of information about the service: a translatable label and a value.
/// </summary>
/// <param name="LabelId">
/// The identifier of the label's message in the plugin's translations, for example
/// <c>diagnostics-clock-fact-now</c>. It must start with the identifier of the plugin followed by a
/// hyphen, so that a plugin cannot shadow a message of the host, and it holds at most 200 characters.
/// Never a sentence: the host does not translate.
/// </param>
/// <param name="Value">The value; formatting it for the user's language and time zone is the interface's job.</param>
public sealed record DiagnosticFact(string LabelId, DiagnosticFactValue Value);
