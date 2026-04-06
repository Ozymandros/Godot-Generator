import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.E2E_BASE_URL ?? "http://127.0.0.1:5010";
const blazorProject =
  "../../GodotGenerator.Blazor/GodotGenerator.Blazor/GodotGenerator.Blazor.csproj";

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
    // Build first so `dotnet run` only starts Kestrel (avoids Playwright webServer timeout on cold compile).
    command: `dotnet build ${blazorProject} -v q && dotnet run --no-build --project ${blazorProject} --urls ${baseURL}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 300_000,
  },
});
