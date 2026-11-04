using AwesomeAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// The trace of the plugins being loaded or called (spec of the plugin management, "Quand Voyager plante à cause d'un
/// plugin"): written while the service runs, held by it alone, read at the next start to find the plugin a crash
/// designates. A test lets go of the file (<see cref="PluginActivity.Dispose"/>, as a crash does) before reading it.
/// </summary>
[TestClass]
public sealed class PluginActivityTests
{
    #region Fields

    private const string TOOL_ID = "fixture-tool";
    private const string MAP_ID = "fixture-map";

    private static readonly PluginKey _tool = new(PluginOrigin.Installed, TOOL_ID);
    private static readonly PluginKey _map = new(PluginOrigin.Shipped, MAP_ID);

    #endregion Fields

    #region Private Methods

    private static string ActivityFileOf(UserPluginFolder user) => new PluginLocations(null, user.Path).ActivityFile!;

    private static PluginActivity Activity(string? file, ILoggerFactory? loggerFactory = null) =>
        new(file, (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<PluginActivity>());

    // Plugins that finished loading, and so may be called.
    private static PluginActivity WithLoaded(string? file, params PluginKey[] plugins) => WithLoaded(Activity(file), plugins);

    private static PluginActivity WithLoaded(PluginActivity activity, params PluginKey[] plugins)
    {
        foreach (var plugin in plugins)
        {
            activity.BeginLoad(plugin);
            activity.EndLoad(plugin, loaded: true);
        }

        return activity;
    }

    // What the trace says once the service lets go of it, as after a crash.
    private static (PluginKey? Loading, IReadOnlyList<PluginKey> Calls) ReadAfterCrash(PluginActivity activity, string file)
    {
        activity.Dispose();
        return PluginStoreFiles.ReadActivity(file);
    }

    private static PluginKey? FindSuspect(string? file) => PluginActivity.FindSuspect(file, NullLogger.Instance);

    private static void WriteActivityFile(string file, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    // A path whose folder cannot be created: a file has its name.
    private static string BlockedFile(UserPluginFolder user)
    {
        Directory.CreateDirectory(user.Path);
        File.WriteAllText(Path.Combine(user.Path, "not-a-folder"), "Traveler A");
        return Path.Combine(user.Path, "not-a-folder", "activity.json");
    }

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public void BeginLoad_PluginStartsLoading_IsTracedAsLoading()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = Activity(file);

        activity.BeginLoad(_tool);

        var (loading, calls) = ReadAfterCrash(activity, file);
        loading.Should().Be(_tool);
        calls.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(true, DisplayName = "loaded")]
    [DataRow(false, DisplayName = "set aside")]
    public void EndLoad_NothingElseGoingOn_LeavesATraceThatNamesNoPlugin(bool loaded)
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = Activity(file);
        activity.BeginLoad(_tool);

        activity.EndLoad(_tool, loaded);

        var (loading, calls) = ReadAfterCrash(activity, file);
        loading.Should().BeNull();
        calls.Should().BeEmpty();
    }

    [TestMethod]
    public void BeginLoad_WhileTheServiceRuns_KeepsTheTraceFromAnyOtherReader()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = Activity(file);

        activity.BeginLoad(_tool);

        var read = () => File.ReadAllText(file);
        read.Should().Throw<IOException>("another service that shares the folder must not take a live trace for a crash");
    }

    [TestMethod]
    public void BeginCall_LoadedPlugin_IsTracedAsCalled()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);

        var traced = activity.BeginCall(TOOL_ID);

        traced.Should().BeTrue();
        var (loading, calls) = ReadAfterCrash(activity, file);
        loading.Should().BeNull();
        calls.Should().Equal(_tool);
    }

    [TestMethod]
    public void BeginCall_TwoPluginsCalledAtOnce_TracesBoth()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool, _map);

        activity.BeginCall(TOOL_ID);
        activity.BeginCall(MAP_ID);

        ReadAfterCrash(activity, file).Calls.Should().BeEquivalentTo([_tool, _map]);
    }

    [TestMethod]
    public void EndCall_LastCallEnds_LeavesATraceThatNamesNoCall()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);

        activity.EndCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void EndCall_OneOfTwoCallsToTheSamePluginEnds_KeepsItTraced()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);
        activity.BeginCall(TOOL_ID);

        activity.EndCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().Equal(_tool);
    }

    [TestMethod]
    public void EndCall_BothCallsToTheSamePluginEnd_LeavesATraceThatNamesNoCall()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);
        activity.BeginCall(TOOL_ID);

        activity.EndCall(TOOL_ID);
        activity.EndCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void EndCall_CallThatWasNeverTraced_ChangesNothing()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool, _map);
        activity.BeginCall(MAP_ID);

        activity.EndCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().Equal(_map);
    }

    [TestMethod]
    public void BeginCall_IdentifierThatNeverLoaded_IsNotTraced()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);

        var traced = activity.BeginCall(PluginCatalog.SERVICE_ID);

        traced.Should().BeFalse();
        ReadAfterCrash(activity, file).Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void BeginCall_PluginThatWasSetAsideAtLoading_IsNotTraced()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = Activity(file);
        activity.BeginLoad(_tool);
        activity.EndLoad(_tool, loaded: false);

        var traced = activity.BeginCall(TOOL_ID);

        traced.Should().BeFalse();
        ReadAfterCrash(activity, file).Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void BeginCall_ManyCallsBeginningAndEndingAtOnce_EndWithATraceThatNamesNoCall()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool, _map);

        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
        {
            var id = index % 2 == 0 ? TOOL_ID : MAP_ID;
            activity.BeginCall(id);
            activity.EndCall(id);
        });

        ReadAfterCrash(activity, file).Calls.Should().BeEmpty();
    }

    [TestMethod]
    public void BeginCall_SamePluginCalledAgain_DoesNotWriteTheTraceAgain()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool, _map);
        activity.BeginCall(TOOL_ID);
        // Let go of the file, then mark it: a write would replace the mark.
        activity.Dispose();
        const string mark = """{ "calls": [ { "origin": "shipped", "id": "fixture-mark" } ] }""";
        File.WriteAllText(file, mark);

        activity.BeginCall(TOOL_ID);
        activity.EndCall(TOOL_ID);

        activity.Dispose();
        File.ReadAllText(file).Should().Be(mark, "the trace still names the same calls");
    }

    [TestMethod]
    public void BeginCall_AnotherPluginCalled_WritesTheTraceAgain()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool, _map);
        activity.BeginCall(TOOL_ID);
        activity.Dispose();
        File.WriteAllText(file, """{ "calls": [ { "origin": "shipped", "id": "fixture-mark" } ] }""");

        activity.BeginCall(MAP_ID);

        ReadAfterCrash(activity, file).Calls.Should().BeEquivalentTo([_tool, _map]);
    }

    [TestMethod]
    public void Stop_CallStillRunning_DeletesTheTrace()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);

        activity.Stop();

        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public void Stop_ThenMoreActivity_WritesNothing()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);
        activity.Stop();

        activity.BeginCall(TOOL_ID);
        activity.BeginLoad(_map);
        activity.EndCall(TOOL_ID);

        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public void Stop_TraceThisServiceNeverWrote_IsLeftToItsWriter()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        WriteActivityFile(file, PluginStoreFiles.ActivityText(_tool, []));
        using var activity = Activity(file);

        activity.Stop();

        File.Exists(file).Should().BeTrue("the trace may be another service's, or a crash's not read yet");
    }

    [TestMethod]
    public void Dispose_CallStillRunning_LeavesTheTraceAsACrashWould()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        var activity = WithLoaded(file, _tool);
        activity.BeginCall(TOOL_ID);

        activity.Dispose();

        FindSuspect(file).Should().Be(_tool);
    }

    [TestMethod]
    public void Activity_WithoutAFile_TracesNothingAndDoesNotThrow()
    {
        using var activity = Activity(file: null);

        var run = () =>
        {
            activity.BeginLoad(_tool);
            activity.EndLoad(_tool, loaded: true);
            activity.BeginCall(TOOL_ID);
            activity.EndCall(TOOL_ID);
            activity.Stop();
        };

        run.Should().NotThrow();
    }

    [TestMethod]
    public void Activity_FolderThatCannotBeCreated_DoesNotThrow()
    {
        using var user = new UserPluginFolder();
        using var activity = Activity(BlockedFile(user));

        var run = () =>
        {
            activity.BeginLoad(_tool);
            activity.EndLoad(_tool, loaded: true);
            activity.BeginCall(TOOL_ID);
            activity.EndCall(TOOL_ID);
            activity.Stop();
        };

        run.Should().NotThrow();
    }

    [TestMethod]
    public void Activity_SeveralWritesThatFail_AreLoggedOnceWithoutThePath()
    {
        using var user = new UserPluginFolder();
        var logs = new CapturingLoggerProvider();
        using var activity = Activity(BlockedFile(user), logs.CreateFactory());

        activity.BeginLoad(_tool);
        activity.EndLoad(_tool, loaded: true);
        activity.BeginCall(TOOL_ID);

        logs.Messages.Should().ContainSingle()
            .Which.Should().StartWith("The trace of the plugins being loaded or called could not be written")
            .And.NotContain(user.Path, "paths hold the user's account name");
    }

    [TestMethod]
    public void Activity_WriteThatFailedThenCanBeDone_IsDoneAtTheNextChange()
    {
        using var user = new UserPluginFolder();
        var file = BlockedFile(user);
        var logs = new CapturingLoggerProvider();
        using var activity = Activity(file, logs.CreateFactory());
        activity.BeginLoad(_tool);
        activity.EndLoad(_tool, loaded: true);
        File.Delete(Path.GetDirectoryName(file)!);

        activity.BeginCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().Equal(_tool);
        logs.Messages.Should().ContainSingle("the failure was said once, and the write that worked says nothing");
    }

    [TestMethod]
    public void Activity_WriteThatFailedWithoutAnyChangeSince_IsDoneAgainAtTheNextChangeEvenToTheSameContent()
    {
        using var user = new UserPluginFolder();
        var file = BlockedFile(user);
        using var activity = Activity(file);
        WithLoaded(activity, _tool, _map);
        activity.BeginCall(TOOL_ID);
        File.Delete(Path.GetDirectoryName(file)!);

        activity.BeginCall(TOOL_ID);

        ReadAfterCrash(activity, file).Calls.Should().Equal(_tool);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Activity_TraceLockedByAnotherProgram_DoesNotThrow()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        WriteActivityFile(file, "{}");
        using var activity = Activity(file);
        using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var run = () =>
            {
                WithLoaded(activity, _tool);
                activity.BeginCall(TOOL_ID);
                activity.EndCall(TOOL_ID);
            };

            run.Should().NotThrow();
        }

        activity.BeginCall(TOOL_ID);
        ReadAfterCrash(activity, file).Calls.Should().Equal(_tool);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Activity_ReadOnlyTrace_DoesNotThrow()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        WriteActivityFile(file, "{}");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        using var activity = Activity(file);

        var run = () =>
        {
            WithLoaded(activity, _tool);
            activity.BeginCall(TOOL_ID);
            activity.EndCall(TOOL_ID);
            activity.Stop();
        };

        run.Should().NotThrow();
    }

    [TestMethod]
    public void FindSuspect_CrashWhileLoading_DesignatesThatPlugin()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = Activity(file))
        {
            activity.BeginLoad(_tool);
        }

        var suspect = FindSuspect(file);

        suspect.Should().Be(_tool);
    }

    [TestMethod]
    public void FindSuspect_CrashWhileLoadingDuringACallToAnother_DesignatesThePluginBeingLoaded()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _map))
        {
            activity.BeginCall(MAP_ID);
            activity.BeginLoad(_tool);
        }

        var suspect = FindSuspect(file);

        suspect.Should().Be(_tool);
    }

    [TestMethod]
    public void FindSuspect_CrashDuringTheOnlyCall_DesignatesThatPlugin()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _tool, _map))
        {
            activity.BeginCall(TOOL_ID);
        }

        var suspect = FindSuspect(file);

        suspect.Should().Be(_tool);
    }

    [TestMethod]
    public void FindSuspect_CrashDuringTwoCallsToTheSamePlugin_DesignatesThatPlugin()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _tool))
        {
            activity.BeginCall(TOOL_ID);
            activity.BeginCall(TOOL_ID);
        }

        var suspect = FindSuspect(file);

        suspect.Should().Be(_tool);
    }

    [TestMethod]
    public void FindSuspect_CrashDuringCallsToTwoPlugins_DesignatesNoneAndSaysWhy()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _tool, _map))
        {
            activity.BeginCall(TOOL_ID);
            activity.BeginCall(MAP_ID);
        }

        var logs = new CapturingLoggerProvider();

        var suspect = PluginActivity.FindSuspect(file, logs.CreateFactory().CreateLogger<PluginActivity>());

        suspect.Should().BeNull();
        logs.Messages.Should().Equal("The service stopped abruptly while 2 plugins were being called: none is suspended on its own");
    }

    [TestMethod]
    public void FindSuspect_CrashWhileNothingWasGoingOn_DesignatesNoneAndSaysNothing()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _tool))
        {
            activity.BeginCall(TOOL_ID);
            activity.EndCall(TOOL_ID);
        }

        var logs = new CapturingLoggerProvider();

        var suspect = PluginActivity.FindSuspect(file, logs.CreateFactory().CreateLogger<PluginActivity>());

        suspect.Should().BeNull();
        logs.Messages.Should().BeEmpty();
        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public void FindSuspect_NoTrace_DesignatesNone()
    {
        using var user = new UserPluginFolder();

        var suspect = FindSuspect(ActivityFileOf(user));

        suspect.Should().BeNull();
    }

    [TestMethod]
    public void FindSuspect_PluginsCannotBeManaged_DesignatesNone()
    {
        var suspect = FindSuspect(file: null);

        suspect.Should().BeNull();
    }

    [TestMethod]
    public void FindSuspect_NormalStopAfterACall_DesignatesNone()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = WithLoaded(file, _tool))
        {
            activity.BeginCall(TOOL_ID);
            activity.Stop();
        }

        var suspect = FindSuspect(file);

        suspect.Should().BeNull();
    }

    [TestMethod]
    [DataRow("this is not json", DisplayName = "not JSON")]
    [DataRow("""{ "loading": { "origin": "borrowed", "id": "fixture-tool" }, "calls": [] }""", DisplayName = "unknown origin")]
    [DataRow("""{ "loading": { "origin": "installed", "id": "..\\escape" }, "calls": [] }""", DisplayName = "identifier that leaves the folder")]
    [DataRow("""{ "calls": "fixture-tool" }""", DisplayName = "calls that are not a list")]
    [DataRow("""[ { "origin": "installed", "id": "fixture-tool" } ]""", DisplayName = "an array instead of an object")]
    public void FindSuspect_DamagedTrace_DesignatesNoneAndDeletesIt(string content)
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        WriteActivityFile(file, content);
        PluginKey? suspect = null;

        var find = () => suspect = FindSuspect(file);

        find.Should().NotThrow();
        suspect.Should().BeNull();
        File.Exists(file).Should().BeFalse("it would be said unreadable again at every start");
    }

    [TestMethod]
    public void FindSuspect_TraceLargerThanOneMegabyte_DesignatesNoneAndDeletesIt()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        WriteActivityFile(file, $$"""{ "loading": { "origin": "installed", "id": "fixture-tool" }, "calls": [] }{{new string(' ', 1024 * 1024)}}""");

        var suspect = FindSuspect(file);

        suspect.Should().BeNull();
        File.Exists(file).Should().BeFalse();
    }

    [TestMethod]
    public void FindSuspect_Trace_IsDeletedOnceRead()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = Activity(file))
        {
            activity.BeginLoad(_tool);
        }

        FindSuspect(file);

        File.Exists(file).Should().BeFalse();
        FindSuspect(file).Should().BeNull("the next start must not designate the plugin again");
    }

    [TestMethod]
    public void FindSuspect_Suspect_IsLoggedWithoutThePath()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = Activity(file))
        {
            activity.BeginLoad(_tool);
        }

        var logs = new CapturingLoggerProvider();

        PluginActivity.FindSuspect(file, logs.CreateFactory().CreateLogger<PluginActivity>());

        logs.Messages.Should().Equal(
            "The service stopped abruptly while plugin fixture-tool (Installed) was loading: it is suspected of the crash");
    }

    [TestMethod]
    public void FindSuspect_TraceHeldByAnotherRunningService_DesignatesNoneAndLeavesIt()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using var running = WithLoaded(file, _tool);
        running.BeginCall(TOOL_ID);
        var logs = new CapturingLoggerProvider();

        var suspect = PluginActivity.FindSuspect(file, logs.CreateFactory().CreateLogger<PluginActivity>());

        suspect.Should().BeNull("the other service traces its own plugins: nothing crashed");
        logs.Messages.Should().Equal(
            "Another service that shares the plugins folder holds the trace of its plugins: no plugin is suspended");
        ReadAfterCrash(running, file).Calls.Should().Equal(_tool);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FindSuspect_TraceThatCanBeReadButNotDeleted_StillDesignatesItsPlugin()
    {
        using var user = new UserPluginFolder();
        var file = ActivityFileOf(user);
        using (var activity = Activity(file))
        {
            activity.BeginLoad(_tool);
        }

        using var antivirus = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);

        var suspect = FindSuspect(file);

        suspect.Should().Be(_tool);
        File.Exists(file).Should().BeTrue("the file could not be deleted");
    }

    #endregion Public Methods
}
