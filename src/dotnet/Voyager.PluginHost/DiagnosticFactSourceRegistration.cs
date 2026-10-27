using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// A source of diagnostic facts in the <see cref="ExtensionRegistry"/>, with who provides it.
/// </summary>
/// <param name="OwnerId">The identifier of the plugin that provides the source, or <see cref="PluginCatalog.SERVICE_ID"/>.</param>
/// <param name="Source">The source.</param>
public sealed record DiagnosticFactSourceRegistration(string OwnerId, IDiagnosticFactSource Source);
