import { dirname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5010";
/** Matches `tools/verify.ps1` / `dotnet` defaults (Release). Overridable via DOTNET_CONFIGURATION. */
const dotnetConfiguration = process.env.DOTNET_CONFIGURATION ?? "Release";

/** Repo root (this file lives in test/GodotGenerator.Blazor.E2E). */
const configDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(configDir, "../..");
const blazorProjectRel = join(
  "GodotGenerator.Blazor",
  "GodotGenerator.Blazor",
  "GodotGenerator.Blazor.csproj",
);
/** Use `/` in shell argv so Windows cmd does not treat `\\` as escapes. */
const blazorProjectForCli = blazorProjectRel.split(sep).join("/");

/**
 * When set (e.g. by tools/verify.ps1 or CI), webServer only runs `dotnet run --no-build` from repoRoot.
 * Avoids `build && run` in one shell (Windows/cmd edge cases) and duplicate WASM builds that can lock tmp-webcil.
 */
const webServerPrebuilt =
  process.env.E2E_WEBSERVER_PREBUILT === "1" || process.env.E2E_WEBSERVER_PREBUILT === "true";

const webServerCommand = webServerPrebuilt
  ? `dotnet run --no-build -c ${dotnetConfiguration} --project ${blazorProjectForCli} --urls ${baseURL}`
  : `dotnet build ${blazorProjectForCli} -c ${dotnetConfiguration} -v q && dotnet run --no-build -c ${dotnetConfiguration} --project ${blazorProjectForCli} --urls ${baseURL}`;

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    cwd: repoRoot,
    command: webServerCommand,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 600_000,
  },
});
