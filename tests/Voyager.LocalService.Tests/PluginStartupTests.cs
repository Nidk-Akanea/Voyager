using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Google.Protobuf;

using Grpc.Core;

using Voyager.Contracts.PluginHost.V1;
using Voyager.LocalService.Api;
using Voyager.PluginHost.Tests;

namespace Voyager.LocalService.Tests;

/// <summary>
/// The plugins after a stop of the service (spec of the plugin management, "Quand Voyager plante à cause d'un
/// plugin"): a normal stop suspends nothing, a crash at loading or during a call suspends the plugin that caused it.
/// The crashes run the real service in a process of its own, which the plugin ends.
/// </summary>
[TestClass]
public sealed class PluginStartupTests
{
    #region Fields

    private const string CRASHES_ON_LOAD_ID = "fixture-crashes-on-load";
    private const string CRASHES_ON_CALL_ID = "fixture-crashes-on-call";
    private const string SLOW_ID = "fixture-slow-source";

    // Long enough for a start of the service on a busy machine of the CI, short enough to fail rather than hang.
    private static readonly TimeSpan _processTimeout = TimeSpan.FromSeconds(90);

    #endregion Fields

    #region Properties

    private static Metadata AuthorizedHeaders => LocalServiceFactory.TokenHeader(LocalServiceFactory.LOCAL_TOKEN);

    #endregion Properties

    #region Private Methods

    // A folder with the shipped plugins (none), the user's plugins with one installed plugin, and the database.
    private static string NewRoot(string pluginId, string fixture)
    {
        var root = Path.Combine(Path.GetTempPath(), $"voyager-crash-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "shipped"));
        new PluginFolder(Path.Combine(root, "user", "installed")).AddPlugin(pluginId, fixture);
        return root;
    }

    private static void DeleteQuietly(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file still held by an antivirus: the temporary folder is left to the operating system's cleanup.
        }
    }

    // A call as the interface makes it, in gRPC-Web over HTTP/1.1, without the client library the interface uses.
    private static async Task CallGetDiagnosticFactsAsync(Uri address, string sourceId)
    {
        var message = new GetDiagnosticFactsRequest { SourceId = sourceId }.ToByteArray();
        var frame = new byte[5 + message.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1), message.Length);
        message.CopyTo(frame, 5);
        using var client = new HttpClient { BaseAddress = address, Timeout = _processTimeout };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/voyager.pluginhost.v1.PluginHostService/GetDiagnosticFacts")
        {
            Content = new ByteArrayContent(frame),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc-web+proto");
        request.Headers.Add("x-grpc-web", "1");
        request.Headers.Add(LocalTokenInterceptor.TOKEN_HEADER, LocalServiceFactory.LOCAL_TOKEN);
        try
        {
            using var response = await client.SendAsync(request);
            await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException)
        {
            // The service died during the call: what the test is waiting for.
        }
    }

    // The service of a second start: it says it listens, suspends the plugin, then stops normally.
    private static async Task RestartAndCheckSuspendedAsync(string root, string pluginId)
    {
        var activityFile = Path.Combine(root, "user", "activity.json");
        using var restarted = ServiceProcess.Start(root);

        var address = await restarted.WaitUntilListeningAsync();
        var exitCode = await restarted.StopAsync();

        address.Should().NotBeNull($"the service starts without the plugin.{Environment.NewLine}{restarted.Output}");
        exitCode.Should().Be(0, $"the service stops normally.{Environment.NewLine}{restarted.Output}");
        restarted.Output.Should().Contain($"Plugin {pluginId} (Installed) is suspended after a crash");
        File.Exists(activityFile).Should().BeFalse("the trace is read once, and a normal stop deletes the new one");
        var plugin = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "user", "state.json")))!["plugins"]!.AsArray()
            .Should().ContainSingle().Subject!;
        plugin["id"]!.GetValue<string>().Should().Be(pluginId);
        plugin["origin"]!.GetValue<string>().Should().Be("installed");
        plugin["enabled"]!.GetValue<bool>().Should().BeTrue();
        plugin["suspended"]!.GetValue<bool>().Should().BeTrue();
    }

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public async Task Stop_WhileAPluginIsBeingCalled_DeletesTheTraceAndSuspendsNothingAtTheNextStart()
    {
        using var plugins = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddPlugin(SLOW_ID, "SlowSource");
        var activityFile = Path.Combine(user.Path, "activity.json");
        using (var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path))
        {
            using var cancellation = new CancellationTokenSource();
            var call = new PluginHostService.PluginHostServiceClient(factory.CreateChannel()).GetDiagnosticFactsAsync(
                new GetDiagnosticFactsRequest { SourceId = SLOW_ID },
                AuthorizedHeaders,
                cancellationToken: cancellation.Token);

            // The service holds the trace while it runs: nothing tells from outside that the call has begun. Half a
            // second is far within the time limit of the call (5 s), after which it would no longer be traced.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await cancellation.CancelAsync();
            var wait = async () => await call;
            await wait.Should().ThrowAsync<RpcException>();
        }

        File.Exists(activityFile).Should().BeFalse("a normal stop deletes the trace");

        using var restarted = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);
        var response = await new PluginHostService.PluginHostServiceClient(restarted.CreateChannel())
            .ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        response.Plugins.Should().ContainSingle().Which.Status.Should().Be(PluginStatus.Loaded);
    }

    [TestMethod]
    public async Task Start_AfterAPluginCrashedTheServiceWhileLoading_SuspendsItAndStarts()
    {
        var root = NewRoot(CRASHES_ON_LOAD_ID, "CrashesOnLoad");
        var activityFile = Path.Combine(root, "user", "activity.json");
        try
        {
            using (var crashed = ServiceProcess.Start(root))
            {
                var exitCode = await crashed.WaitForExitAsync();

                exitCode.Should().NotBeNull($"the plugin ends the service as it loads.{Environment.NewLine}{crashed.Output}");
                exitCode.Should().NotBe(0, crashed.Output);
            }

            File.Exists(activityFile).Should().BeTrue("a crash leaves the trace of the plugin being loaded");
            JsonNode.Parse(File.ReadAllText(activityFile))!["loading"]!["id"]!.GetValue<string>().Should().Be(CRASHES_ON_LOAD_ID);

            await RestartAndCheckSuspendedAsync(root, CRASHES_ON_LOAD_ID);
        }
        finally
        {
            DeleteQuietly(root);
        }
    }

    [TestMethod]
    public async Task Start_AfterAPluginCrashedTheServiceDuringACall_SuspendsItAndStarts()
    {
        var root = NewRoot(CRASHES_ON_CALL_ID, "CrashesOnCall");
        var activityFile = Path.Combine(root, "user", "activity.json");
        try
        {
            using (var crashed = ServiceProcess.Start(root))
            {
                var address = await crashed.WaitUntilListeningAsync();
                address.Should().NotBeNull($"the plugin loads well.{Environment.NewLine}{crashed.Output}");

                await CallGetDiagnosticFactsAsync(address!, CRASHES_ON_CALL_ID);
                var exitCode = await crashed.WaitForExitAsync();

                exitCode.Should().NotBeNull($"the plugin ends the service when called.{Environment.NewLine}{crashed.Output}");
                exitCode.Should().NotBe(0, crashed.Output);
            }

            var trace = JsonNode.Parse(File.ReadAllText(activityFile))!.AsObject();
            trace.ContainsKey("loading").Should().BeFalse();
            trace["calls"]!.AsArray().Select(call => call!["id"]!.GetValue<string>()).Should().Equal(CRASHES_ON_CALL_ID);

            await RestartAndCheckSuspendedAsync(root, CRASHES_ON_CALL_ID);
        }
        finally
        {
            DeleteQuietly(root);
        }
    }

    #endregion Public Methods

    #region Classes

    // The real service, built next to the tests, in a process of its own: a plugin that ends the process ends only it.
    private sealed class ServiceProcess : IDisposable
    {
        #region Fields

        private const string LISTENING_PREFIX = "VOYAGER_LISTENING ";

        private readonly Process _process;
        private readonly StringBuilder _output = new();
        private readonly TaskCompletionSource<Uri> _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion Fields

        #region Constructors

        private ServiceProcess(Process process) => _process = process;

        #endregion Constructors

        #region Properties

        /// <summary>What the service wrote on its outputs so far: its logs.</summary>
        public string Output
        {
            get
            {
                lock (_output)
                {
                    return _output.ToString();
                }
            }
        }

        #endregion Properties

        #region Private Methods

        // The dotnet host that runs the tests, so that the service runs on the same runtime.
        private static string DotnetHost() =>
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host) ? host : "dotnet";

        private void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_output)
            {
                _output.AppendLine(line);
            }

            if (line.StartsWith(LISTENING_PREFIX, StringComparison.Ordinal))
            {
                _listening.TrySetResult(new Uri(line[LISTENING_PREFIX.Length..]));
            }
        }

        #endregion Private Methods

        #region Public Methods

        public static ServiceProcess Start(string root)
        {
            var start = new ProcessStartInfo(DotnetHost(), [Path.Combine(AppContext.BaseDirectory, "Voyager.LocalService.dll")])
            {
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Nothing of the machine's own settings reaches the service: where it listens, logs or finds plugins.
            foreach (var inherited in new[] { "ASPNETCORE_HTTP_PORTS", "ASPNETCORE_HTTPS_PORTS", "OTEL_EXPORTER_OTLP_ENDPOINT" })
            {
                start.Environment.Remove(inherited);
            }

            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            start.Environment["DOTNET_ENVIRONMENT"] = "Production";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
            start.Environment["Seq__ServerUrl"] = string.Empty;
            start.Environment["Voyager__LogDirectory"] = string.Empty;
            start.Environment["Voyager__LocalToken"] = LocalServiceFactory.LOCAL_TOKEN;
            start.Environment["Voyager__DatabasePath"] = Path.Combine(root, "voyager.db");
            start.Environment["Voyager__ShippedPluginsDirectory"] = Path.Combine(root, "shipped");
            start.Environment["Voyager__UserPluginsDirectory"] = Path.Combine(root, "user");
            // The test stops the service as the desktop app does: by closing its standard input.
            start.Environment["Voyager__StopWhenInputCloses"] = "true";

            var service = new ServiceProcess(new Process { StartInfo = start });
            service._process.OutputDataReceived += (_, line) => service.Append(line.Data);
            service._process.ErrorDataReceived += (_, line) => service.Append(line.Data);
            service._process.Start();
            service._process.BeginOutputReadLine();
            service._process.BeginErrorReadLine();
            return service;
        }

        /// <summary>Waits until the service says where it listens; null if it ends or the time runs out first.</summary>
        public async Task<Uri?> WaitUntilListeningAsync()
        {
            var first = await Task.WhenAny(_listening.Task, _process.WaitForExitAsync(), Task.Delay(_processTimeout));
            return first == _listening.Task ? await _listening.Task : null;
        }

        /// <summary>Waits until the service ends by itself; null if the time runs out first.</summary>
        public async Task<int?> WaitForExitAsync()
        {
            using var timeout = new CancellationTokenSource(_processTimeout);
            try
            {
                await _process.WaitForExitAsync(timeout.Token);
                return _process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>Stops the service as the desktop app does, and waits until it ends; null if the time runs out first.</summary>
        public Task<int?> StopAsync()
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
            }

            return WaitForExitAsync();
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already gone.
            }

            _process.Dispose();
        }

        #endregion Public Methods
    }

    #endregion Classes
}
