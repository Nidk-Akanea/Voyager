using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost;
using Voyager.Sdk.Diagnostics;

using HostContractRole = Voyager.PluginHost.ContractRole;
using HostContractUse = Voyager.PluginHost.ContractUse;
using HostContractUseState = Voyager.PluginHost.ContractUseState;
using HostOrigin = Voyager.PluginHost.PluginOrigin;
using HostRejectionReason = Voyager.PluginHost.PluginRejectionReason;
using HostStatus = Voyager.PluginHost.PluginStatus;
using HostVerification = Voyager.PluginHost.PluginVerification;
using WireContractRole = Voyager.Contracts.PluginHost.V1.ContractRole;
using WireContractUse = Voyager.Contracts.PluginHost.V1.ContractUse;
using WireContractUseState = Voyager.Contracts.PluginHost.V1.ContractUseState;
using WireOrigin = Voyager.Contracts.PluginHost.V1.PluginOrigin;
using WireRejectionReason = Voyager.Contracts.PluginHost.V1.PluginRejectionReason;
using WireStatus = Voyager.Contracts.PluginHost.V1.PluginStatus;
using WireVerification = Voyager.Contracts.PluginHost.V1.PluginVerification;

namespace Voyager.GroupServer.Api;

/// <summary>
/// Tells the user interface which plugins the server runs and what their diagnostic sources report
/// (proto/voyager/pluginhost/v1/pluginhost.proto). Only reads: the server has no installed plugins yet, so examining
/// a package is not served (the base class answers "unimplemented"), and the fields of the user's requests stay empty.
/// Managing plugins on the server comes with its own spec (ADR-0020).
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
            Status = ToMessage(state.Status),
            RejectionReason = state.RejectionReason is { } reason ? ToMessage(reason) : WireRejectionReason.Unspecified,
            UiModule = state.UiModule ?? string.Empty,
            Origin = ToMessage(state.Origin),
            Verification = ToMessage(state.Verification),
            HasDotnet = state.HasDotnet,
            HasUi = state.HasUi,
        };

        message.Provides.AddRange(state.Provides);
        message.Contracts.AddRange(state.Contracts.Select(ToMessage));
        return message;
    }

    private static WireStatus ToMessage(HostStatus status) => status switch
    {
        HostStatus.Loaded => WireStatus.Loaded,
        HostStatus.Rejected => WireStatus.Rejected,
        HostStatus.Disabled => WireStatus.Disabled,
        HostStatus.Suspended => WireStatus.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "A plugin status has no counterpart in the gRPC message."),
    };

    private static WireVerification ToMessage(HostVerification verification) => verification switch
    {
        HostVerification.GuaranteedByApplication => WireVerification.GuaranteedByApplication,
        HostVerification.Unverified => WireVerification.Unverified,
        _ => throw new ArgumentOutOfRangeException(nameof(verification), verification, "A plugin verification has no counterpart in the gRPC message."),
    };

    private static WireOrigin ToMessage(HostOrigin origin) => origin switch
    {
        HostOrigin.Shipped => WireOrigin.Shipped,
        HostOrigin.Installed => WireOrigin.Installed,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "A plugin origin has no counterpart in the gRPC message."),
    };

    private static WireRejectionReason ToMessage(HostRejectionReason reason) => reason switch
    {
        HostRejectionReason.ManifestInvalid => WireRejectionReason.ManifestInvalid,
        HostRejectionReason.DuplicateId => WireRejectionReason.DuplicateId,
        HostRejectionReason.DuplicateOfShipped => WireRejectionReason.DuplicateOfShipped,
        HostRejectionReason.ContractNotSupported => WireRejectionReason.ContractNotSupported,
        HostRejectionReason.LoadFailed => WireRejectionReason.LoadFailed,
        HostRejectionReason.UndeclaredRegistration => WireRejectionReason.UndeclaredRegistration,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A rejection reason has no counterpart in the gRPC message."),
    };

    private static WireContractUse ToMessage(HostContractUse use) => new()
    {
        Contract = use.Contract,
        // Never negative: the manifest reader only accepts versions from 1.
        Version = (uint)use.Version,
        Role = ToMessage(use.Role),
        State = ToMessage(use.State),
    };

    private static WireContractRole ToMessage(HostContractRole role) => role switch
    {
        HostContractRole.Provided => WireContractRole.Provided,
        HostContractRole.Required => WireContractRole.Required,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "A contract role has no counterpart in the gRPC message."),
    };

    private static WireContractUseState ToMessage(HostContractUseState state) => state switch
    {
        HostContractUseState.Supported => WireContractUseState.Supported,
        HostContractUseState.Deprecated => WireContractUseState.Deprecated,
        HostContractUseState.Obsolete => WireContractUseState.Obsolete,
        HostContractUseState.Unknown => WireContractUseState.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "A contract use state has no counterpart in the gRPC message."),
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
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "A fact outcome has no counterpart in the gRPC message."),
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
                throw new ArgumentOutOfRangeException(nameof(fact), fact.Value.Kind, "A fact kind has no counterpart in the gRPC message.");
        }

        return message;
    }

    #endregion Private Methods

    #region Public Methods

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

    /// <inheritdoc />
    public override Task<ListPluginsResponse> ListPlugins(ListPluginsRequest request, ServerCallContext context)
    {
        var response = new ListPluginsResponse();
        response.Plugins.AddRange(catalog.Plugins.Select(ToMessage));
        return Task.FromResult(response);
    }

    #endregion Public Methods
}
