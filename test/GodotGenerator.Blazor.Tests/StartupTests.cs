using Microsoft.Playwright;
using System.Diagnostics;
using Xunit;

namespace GodotGenerator.Blazor.Tests
{
    public class StartupTests
    {
        private const string BaseUrl = "http://127.0.0.1:5044";
        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        private static readonly string BlazorProjectPath = Path.Combine(
            RepoRoot,
            "GodotGenerator.Blazor",
            "GodotGenerator.Blazor",
            "GodotGenerator.Blazor.csproj");
        private static readonly string ElectronProjectPath = Path.Combine(RepoRoot, "electron");

        [Fact]
        public async Task AppStartsWithoutErrors()
        {
            // Optional E2E smoke test: run only when explicitly enabled.
            if (Environment.GetEnvironmentVariable("RUN_PLAYWRIGHT_STARTUP_TEST") != "1")
            {
                return;
            }

            using var appProcess = StartLocalBlazorHost();
            try
            {
                await WaitUntilServerIsReadyAsync(BaseUrl, TimeSpan.FromSeconds(30));

                var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                var errors = new List<string>();
                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        errors.Add(msg.Text);
                    }
                };

                await page.GotoAsync(BaseUrl);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                if (Environment.GetEnvironmentVariable("ALLOW_RESOURCE_CONSOLE_ERRORS") == "1")
                {
                    errors = errors
                        .Where(static e => !e.StartsWith("Failed to load resource", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                Assert.Empty(errors);
            }
            finally
            {
                if (!appProcess.HasExited)
                {
                    appProcess.Kill(entireProcessTree: true);
                    appProcess.WaitForExit(5000);
                }
            }
        }

        [Fact]
        public async Task ElectronDesktopStartsBackendAndServesUi()
        {
            // Optional desktop E2E smoke test: run only when explicitly enabled.
            if (Environment.GetEnvironmentVariable("RUN_PLAYWRIGHT_ELECTRON_STARTUP_TEST") != "1")
            {
                return;
            }

            using var electronProcess = StartElectronDesktop();
            try
            {
                var readyMarker = Environment.GetEnvironmentVariable("ELECTRON_READY_MARKER");
                if (string.IsNullOrWhiteSpace(readyMarker))
                {
                    readyMarker = "[BackendLifecycle] Backend is ready on pipe:";
                }

                await WaitForProcessOutputAsync(
                    electronProcess,
                    readyMarker,
                    TimeSpan.FromSeconds(60));

                await WaitUntilServerIsReadyAsync(BaseUrl, TimeSpan.FromSeconds(30));

                var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                var errors = new List<string>();
                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        errors.Add(msg.Text);
                    }
                };

                await page.GotoAsync(BaseUrl);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                if (Environment.GetEnvironmentVariable("ALLOW_RESOURCE_CONSOLE_ERRORS") == "1")
                {
                    errors = errors
                        .Where(static e => !e.StartsWith("Failed to load resource", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                Assert.Empty(errors);
            }
            finally
            {
                if (!electronProcess.HasExited)
                {
                    electronProcess.Kill(entireProcessTree: true);
                    electronProcess.WaitForExit(5000);
                }
            }
        }

        private static Process StartLocalBlazorHost()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{BlazorProjectPath}\" --urls {BaseUrl}",
                WorkingDirectory = RepoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start local Blazor host process.");
            return process;
        }

        private static Process StartElectronDesktop()
        {
            var command = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
            var electronScript = Environment.GetEnvironmentVariable("ELECTRON_START_SCRIPT");
            if (string.IsNullOrWhiteSpace(electronScript))
            {
                electronScript = "dev:5044";
            }

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = $"run {electronScript}",
                WorkingDirectory = ElectronProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            psi.Environment["GODOT_BLAZOR_URL"] = BaseUrl;

            var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Electron desktop process.");
            return process;
        }

        private static async Task WaitForProcessOutputAsync(Process process, string marker, TimeSpan timeout)
        {
            var output = new List<string>();
            var markerReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnLine(string? line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                lock (output)
                {
                    output.Add(line);
                }

                if (line.Contains(marker, StringComparison.Ordinal))
                {
                    markerReached.TrySetResult(true);
                }
            }

            DataReceivedEventHandler stdoutHandler = (_, e) => OnLine(e.Data);
            DataReceivedEventHandler stderrHandler = (_, e) => OnLine(e.Data);
            process.OutputDataReceived += stdoutHandler;
            process.ErrorDataReceived += stderrHandler;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"Process exited before marker '{marker}' was observed. Exit code: {process.ExitCode}. Captured output: {string.Join(Environment.NewLine, output)}");
                }

                var completed = await Task.WhenAny(markerReached.Task, Task.Delay(timeout));
                if (completed == markerReached.Task)
                {
                    return;
                }
            }
            finally
            {
                process.OutputDataReceived -= stdoutHandler;
                process.ErrorDataReceived -= stderrHandler;
            }

            throw new TimeoutException($"Timed out waiting for process marker '{marker}'. Captured output:{Environment.NewLine}{string.Join(Environment.NewLine, output)}");
        }

        private static async Task WaitUntilServerIsReadyAsync(string baseUrl, TimeSpan timeout)
        {
            using var client = new HttpClient();
            var start = DateTimeOffset.UtcNow;

            while (DateTimeOffset.UtcNow - start < timeout)
            {
                try
                {
                    using var response = await client.GetAsync(baseUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch
                {
                    // Server not ready yet.
                }

                await Task.Delay(250);
            }

            throw new TimeoutException($"Timed out waiting for local Blazor host at {baseUrl}.");
        }
    }
}
