using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost;
using Voyager.PluginHost.Packages;
using Voyager.Sdk.Diagnostics;
using HostContractRole = Voyager.PluginHost.ContractRole;
using HostContractUse = Voyager.PluginHost.ContractUse;
using HostContractUseState = Voyager.PluginHost.ContractUseState;
using HostInstallKind = Voyager.PluginHost.Packages.PackageInstallKind;
using HostOrigin = Voyager.PluginHost.PluginOrigin;
using HostPackageRefusal = Voyager.PluginHost.Packages.PackageRefusal;
using HostRejectionReason = Voyager.PluginHost.PluginRejectionReason;
using HostRequestAction = Voyager.PluginHost.PluginRequestAction;
using HostRequestFailure = Voyager.PluginHost.PluginRequestFailure;
using HostStatus = Voyager.PluginHost.PluginStatus;
using HostVerification = Voyager.PluginHost.PluginVerification;
using WireContractRole = Voyager.Contracts.PluginHost.V1.ContractRole;
using WireContractUse = Voyager.Contracts.PluginHost.V1.ContractUse;
using WireContractUseState = Voyager.Contracts.PluginHost.V1.ContractUseState;
using WireInstallKind = Voyager.Contracts.PluginHost.V1.PackageInstallKind;
using WireOrigin = Voyager.Contracts.PluginHost.V1.PluginOrigin;
using WirePackageRefusal = Voyager.Contracts.PluginHost.V1.PackageRefusal;
using WireRejectionReason = Voyager.Contracts.PluginHost.V1.PluginRejectionReason;
using WireRequestAction = Voyager.Contracts.PluginHost.V1.PluginRequestAction;
using WireRequestFailure = Voyager.Contracts.PluginHost.V1.PluginRequestFailure;
using WireStatus = Voyager.Contracts.PluginHost.V1.PluginStatus;
using WireVerification = Voyager.Contracts.PluginHost.V1.PluginVerification;

namespace Voyager.LocalService.Api;

/// <summary>
/// Tells the user interface which plugins run, what the user asked for them and what their diagnostic sources report
/// (proto/voyager/pluginhost/v1/pluginhost.proto). A technical service over the plugin host: it only reads, and no
/// call changes a plugin (ADR-0016, decision 4).
/// </summary>
/// <param name="catalog">The plugins found at startup.</param>
/// <param name="store">What the service remembers of the plugins, and the requests waiting for the next start.</param>
/// <param name="packages">Examines the packages the desktop app prepared.</param>
/// <param name="collector">Asks the sources of diagnostic facts, with a time limit for each.</param>
/// <param name="logger">Where the service writes the outcome of an examination: fingerprints and identifiers, never a path.</param>
public sealed class PluginHostGrpcService(
    PluginCatalog catalog,
    PluginStore store,
    PluginPackages packages,
    DiagnosticFactCollector collector,
    ILogger<PluginHostGrpcService> logger)
    : PluginHostService.PluginHostServiceBase
{
    #region Private Methods

    private static Plugin ToMessage(PluginState state, PluginRecord? record, PluginRequest? pending)
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
            InstalledAt = record?.InstalledAt is { } installedAt ? Timestamp.FromDateTimeOffset(installedAt.ToDateTimeOffset()) : null,
            PackageName = record?.PackageName ?? string.Empty,
            PackageSha256 = record?.PackageSha256 ?? string.Empty,
            PendingRequest = pending is null ? WireRequestAction.Unspecified : ToMessage(pending.Action),
            FailedRequest = record?.LastError is { } failed ? ToMessage(failed.Action) : WireRequestAction.Unspecified,
            FailedRequestReason = record?.LastError is { } error ? ToMessage(error.Reason) : WireRequestFailure.Unspecified,
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
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "A plugin status has no counterpart in the gRPC message."),
    };

    private static WireVerification ToMessage(HostVerification verification) => verification switch
    {
        HostVerification.GuaranteedByApplication => WireVerification.GuaranteedByApplication,
        HostVerification.Unverified => WireVerification.Unverified,
        _ => throw new ArgumentOutOfRangeException(nameof(verification), verification, "A plugin verification has no counterpart in the gRPC message."),
    };

    private static WirePackageRefusal ToMessage(HostPackageRefusal refusal) => refusal switch
    {
        HostPackageRefusal.Unreadable => WirePackageRefusal.Unreadable,
        HostPackageRefusal.ManifestInvalid => WirePackageRefusal.ManifestInvalid,
        HostPackageRefusal.DeclaredFileMissing => WirePackageRefusal.DeclaredFileMissing,
        HostPackageRefusal.ContractNotSupported => WirePackageRefusal.ContractNotSupported,
        HostPackageRefusal.ForbiddenEntry => WirePackageRefusal.ForbiddenEntry,
        HostPackageRefusal.NotEnoughSpace => WirePackageRefusal.NotEnoughSpace,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "A package refusal has no counterpart in the gRPC message."),
    };

    private static WireInstallKind ToMessage(HostInstallKind kind) => kind switch
    {
        HostInstallKind.New => WireInstallKind.New,
        HostInstallKind.Upgrade => WireInstallKind.Upgrade,
        HostInstallKind.Downgrade => WireInstallKind.Downgrade,
        HostInstallKind.Reinstall => WireInstallKind.Reinstall,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A kind of installation has no counterpart in the gRPC message."),
    };

    private static ExaminePackageResponse ToMessage(PackageExamination examination)
    {
        var message = new ExaminePackageResponse
        {
            Sha256 = examination.Sha256,
            Size = (ulong)examination.Size,
            Refusal = examination.Refusal is { } refusal ? ToMessage(refusal) : WirePackageRefusal.Unspecified,
            Id = examination.Manifest?.Id ?? string.Empty,
            Version = examination.Manifest?.Version ?? string.Empty,
            HasDotnet = examination.Manifest?.DotnetAssembly is not null,
            HasUi = examination.Manifest?.UiModule is not null,
            Verification = ToMessage(PackageExamination.Verification),
            DuplicateOfShipped = examination.DuplicateOfShipped,
            InstalledVersion = examination.InstalledVersion ?? string.Empty,
            InstallKind = examination.Manifest is null ? WireInstallKind.Unspecified : ToMessage(examination.Kind),
        };
        message.Contracts.AddRange(examination.Contracts.Select(ToMessage));
        return message;
    }

    private static WireOrigin ToMessage(HostOrigin origin) => origin switch
    {
        HostOrigin.Shipped => WireOrigin.Shipped,
        HostOrigin.Installed => WireOrigin.Installed,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "A plugin origin has no counterpart in the gRPC message."),
    };

    private static WireRequestAction ToMessage(HostRequestAction action) => action switch
    {
        HostRequestAction.Install => WireRequestAction.Install,
        HostRequestAction.Uninstall => WireRequestAction.Uninstall,
        HostRequestAction.Enable => WireRequestAction.Enable,
        HostRequestAction.Disable => WireRequestAction.Disable,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "A request action has no counterpart in the gRPC message."),
    };

    private static WireRequestFailure ToMessage(HostRequestFailure failure) => failure switch
    {
        HostRequestFailure.NotFound => WireRequestFailure.NotFound,
        HostRequestFailure.NotAllowed => WireRequestFailure.NotAllowed,
        HostRequestFailure.FileSystemError => WireRequestFailure.FileSystemError,
        HostRequestFailure.NotSupported => WireRequestFailure.NotSupported,
        HostRequestFailure.PackageMissing => WireRequestFailure.PackageMissing,
        HostRequestFailure.PackageChanged => WireRequestFailure.PackageChanged,
        HostRequestFailure.PackageRefused => WireRequestFailure.PackageRefused,
        HostRequestFailure.NotEnoughSpace => WireRequestFailure.NotEnoughSpace,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "A request failure has no counterpart in the gRPC message."),
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
    public override async Task<ExaminePackageResponse> ExaminePackage(ExaminePackageRequest request, ServerCallContext context)
    {
        if (store.Locations.StagingDirectory is null)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Plugins cannot be managed: there is no staging folder."));
        }

        if (!PluginPackages.IsValidStagedName(request.StagedName))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "This is not the name of a prepared package."));
        }

        PackageExamination examination;
        try
        {
            examination = await packages.ExamineAsync(request.StagedName, context.CancellationToken);
        }
        catch (FileNotFoundException)
        {
            // The name is random and checked above: it says nothing of the user.
            throw new RpcException(new Status(StatusCode.NotFound, $"There is no prepared package {request.StagedName}."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Its kind and its code only: the message of the exception holds paths.
            LocalServiceLog.PackageExaminationFailed(logger, $"{exception.GetType().Name} (0x{exception.HResult:X8})");
            throw new RpcException(new Status(StatusCode.Unavailable, "The package could not be examined."));
        }

        // Identifiers and versions only: the manifest reader checked them, and nothing else of the package is written.
        switch (examination)
        {
            case { Refusal: { } refusal }:
                LocalServiceLog.PackageExaminedRefused(logger, examination.Sha256, refusal);
                break;
            case { Manifest: { } manifest }:
                LocalServiceLog.PackageExaminedAccepted(logger, examination.Sha256, manifest.Id, manifest.Version);
                break;
        }

        return ToMessage(examination);
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

    /// <inheritdoc />
    public override Task<ListPluginsResponse> ListPlugins(ListPluginsRequest request, ServerCallContext context)
    {
        var pending = store.ReadPendingRequestByPlugin();
        var response = new ListPluginsResponse();
        response.Plugins.AddRange(catalog.Plugins.Select(state =>
        {
            // By folder, as the requests and the store name a plugin: a stray copy that claims the identifier of another
            // plugin never shows what was asked for that one.
            var key = new PluginKey(state.Origin, state.Folder);
            return ToMessage(state, store.Find(key), pending.GetValueOrDefault(key));
        }));
        return Task.FromResult(response);
    }

    #endregion Public Methods
}
