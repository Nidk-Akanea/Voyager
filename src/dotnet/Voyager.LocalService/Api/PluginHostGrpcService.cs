using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost;
using Voyager.Sdk.Diagnostics;

using HostRejectionReason = Voyager.PluginHost.PluginRejectionReason;
using HostStatus = Voyager.PluginHost.PluginStatus;
using WireRejectionReason = Voyager.Contracts.PluginHost.V1.PluginRejectionReason;
using WireStatus = Voyager.Contracts.PluginHost.V1.PluginStatus;

namespace Voyager.LocalService.Api;

/// <summary>
/// Tells the user interface which plugins run and what their diagnostic sources report
/// (proto/voyager/pluginhost/v1/pluginhost.proto). A technical service over the plugin host.
/// </summary>
/// <param name="catalog">The plugins found at startup.</param>
/// <param name="collector">Asks the sources of diagnostic facts, with a time limit for each.</param>
public sealed class PluginHostGrpcService(PluginCatalog catalog, DiagnosticFactCollector collector)
    : PluginHostService.PluginHostServiceBase
{
    #region Private Methods

    private static Plugin ToMessage(PluginState state)
    {
        var message = new Plugin
        {
            Folder = state.Folder,
            Id = state.Id,
            Version = state.Version,
            Status = state.Status == HostStatus.Loaded ? WireStatus.Loaded : WireStatus.Rejected,
            RejectionReason = state.RejectionReason is { } reason ? ToMessage(reason) : WireRejectionReason.Unspecified,
            UiModule = state.UiModule ?? string.Empty,
        };

        message.Provides.AddRange(state.Provides);
        return message;
    }

    private static WireRejectionReason ToMessage(HostRejectionReason reason) => reason switch
    {
        HostRejectionReason.ManifestInvalid => WireRejectionReason.ManifestInvalid,
        HostRejectionReason.DuplicateId => WireRejectionReason.DuplicateId,
        HostRejectionReason.SdkIncompatible => WireRejectionReason.SdkIncompatible,
        HostRejectionReason.LoadFailed => WireRejectionReason.LoadFailed,
        HostRejectionReason.UndeclaredRegistration => WireRejectionReason.UndeclaredRegistration,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A rejection reason has no contract counterpart."),
    };

    private static SourceFacts ToMessage(DiagnosticFactSourceResult result)
    {
        var message = new SourceFacts
        {
            SourceId = result.SourceId,
            Outcome = result.Outcome switch
            {
                DiagnosticFactOutcome.Succeeded => FactsOutcome.Succeeded,
                DiagnosticFactOutcome.Failed => FactsOutcome.Failed,
                DiagnosticFactOutcome.TimedOut => FactsOutcome.TimedOut,
                DiagnosticFactOutcome.Invalid => FactsOutcome.Invalid,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "An outcome has no contract counterpart."),
            },
            Truncated = result.Truncated,
        };

        message.Facts.AddRange(result.Facts.Select(ToMessage));
        return message;
    }

    private static Fact ToMessage(DiagnosticFact fact)
    {
        var message = new Fact { LabelId = fact.LabelId };
        switch (fact.Value.Kind)
        {
            case DiagnosticFactKind.Text:
                message.Text = fact.Value.Text;
                break;
            case DiagnosticFactKind.Number:
                message.Number = fact.Value.Number;
                break;
            case DiagnosticFactKind.Instant:
                message.Instant = Timestamp.FromDateTimeOffset(fact.Value.Instant.ToDateTimeOffset());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fact), fact.Value.Kind, "A fact kind has no contract counterpart.");
        }

        return message;
    }

    #endregion Private Methods

    #region Public Methods

    /// <inheritdoc />
    public override Task<ListPluginsResponse> ListPlugins(ListPluginsRequest request, ServerCallContext context)
    {
        var response = new ListPluginsResponse();
        response.Plugins.AddRange(catalog.Plugins.Select(ToMessage));
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override async Task<GetDiagnosticFactsResponse> GetDiagnosticFacts(
        GetDiagnosticFactsRequest request,
        ServerCallContext context)
    {
        var sourceId = request.SourceId.Length == 0 ? null : request.SourceId;
        var results = await collector.CollectAsync(sourceId, context.CancellationToken);
        if (sourceId is not null && results.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"No diagnostic fact source has the identifier '{sourceId}'."));
        }

        var response = new GetDiagnosticFactsResponse();
        response.Sources.AddRange(results.Select(ToMessage));
        return response;
    }

    #endregion Public Methods
}
