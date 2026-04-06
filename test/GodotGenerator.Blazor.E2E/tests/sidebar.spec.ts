import { expect, test, type Page } from "@playwright/test";

/** Left sidebar column (Unity-style ~220px strip). */
function navColumn(page: Page) {
  return page.locator(".app-shell__sidebar").first();
}

const generateLinks = [
  "Scenes",
  "Code",
  "Text",
  "Image",
  "Sprites",
  "Animations",
  "Audio",
  "Video",
  "Godot UI",
  "Godot Physics",
  "Godot Project",
] as const;

const appLinks = ["Home", "Dashboard", "Logs"] as const;

test.describe("Blazor shell", () => {
  test("skip link targets main landmark", async ({ page }) => {
    await page.goto("/");
    const skip = page.locator(".app-skip-link");
    await expect(skip).toHaveAttribute("href", "#main-content");
    await expect(page.locator("#main-content")).toBeVisible();
  });

  test("opens and sidebar lists main routes", async ({ page }) => {
    await page.goto("/");

    await expect(page.locator("header").getByText("Godot Generator", { exact: true })).toBeVisible();

    const nav = navColumn(page);
    await expect(nav.getByRole("link", { name: /Configuration/i })).toBeVisible();

    for (const name of generateLinks) {
      await expect(nav.getByRole("link", { name, exact: true })).toBeVisible();
    }
    for (const name of appLinks) {
      await expect(nav.getByRole("link", { name, exact: true })).toBeVisible();
    }

    const repo = nav.getByRole("link", { name: /Repository/i });
    await expect(repo).toBeVisible();
    await expect(repo).toHaveAttribute("href", "https://github.com/Ozymandros/Godot-Generator-Avalonia");
  });

  test("sidebar links use expected hrefs", async ({ page }) => {
    await page.goto("/");

    const nav = navColumn(page);
    await expect(nav.getByRole("link", { name: /Configuration/i })).toHaveAttribute("href", "/settings");
    await expect(nav.getByRole("link", { name: "Home", exact: true })).toHaveAttribute("href", "/");
    await expect(nav.getByRole("link", { name: "Scenes", exact: true })).toHaveAttribute(
      "href",
      "/generate/scenes",
    );
    await expect(nav.getByRole("link", { name: "Godot Project", exact: true })).toHaveAttribute(
      "href",
      "/generate/godot-project",
    );
  });
});
