namespace Voyager.PluginHost;

/// <summary>
/// The last request for a plugin that could not be applied, and why.
/// </summary>
/// <param name="Action">What the request asked.</param>
/// <param name="Reason">Why it could not be applied.</param>
public sealed record PluginRequestError(PluginRequestAction Action, PluginRequestFailure Reason);
