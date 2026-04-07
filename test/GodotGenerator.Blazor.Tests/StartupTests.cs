using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace GodotGenerator.Blazor.Tests
{
    public class StartupTests
    {
        [Fact]
        public async Task AppStartsWithoutErrors()
        {
            var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            // Navigate to the app
            await page.GotoAsync("http://localhost:5044");

            // Check for console errors
            var errors = new List<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                {
                    errors.Add(msg.Text);
                }
            };

            // Wait for the app to load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert no console errors
            Assert.Empty(errors);
        }
    }
}