using System.Net;
using System.Security.Claims;
using FitMind.Data;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class GoogleLoginTests(FitMindFactory factory)
{
    [Fact]
    public async Task Verified_google_account_creates_user_profile_and_external_login()
    {
        var email = $"google-{Guid.NewGuid():N}@example.test";
        var key = Guid.NewGuid().ToString("N");
        using var client = ExternalClient(email, key, true);
        var response = await client.GetAsync("/Account/ExternalLoginCallback?returnUrl=https://example.test");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Profile/Setup", response.Headers.Location?.OriginalString);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        Assert.True(user.EmailConfirmed);
        Assert.True(await db.UserProfiles.AnyAsync(x => x.UserId == user.Id && !x.OnboardingCompleted));
        Assert.True(await db.UserLogins.AnyAsync(x => x.UserId == user.Id && x.ProviderKey == key));
    }

    [Fact]
    public async Task Unverified_google_email_does_not_create_user()
    {
        var email = $"google-unverified-{Guid.NewGuid():N}@example.test";
        using var client = ExternalClient(email, Guid.NewGuid().ToString("N"), false);
        var response = await client.GetAsync("/Account/ExternalLoginCallback");
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
    }

    [Fact]
    public async Task Google_cannot_link_to_locked_account_and_bypass_lockout()
    {
        var email = $"google-locked-{Guid.NewGuid():N}@example.test";
        var user = await factory.CreateCompletedUserAsync(email, "test-password-123");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var saved = await users.FindByIdAsync(user.Id);
            Assert.True((await users.SetLockoutEnabledAsync(saved!, true)).Succeeded);
            Assert.True((await users.SetLockoutEndDateAsync(saved!, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        }
        using var client = ExternalClient(email, Guid.NewGuid().ToString("N"), true);
        var response = await client.GetAsync("/Account/ExternalLoginCallback");
        Assert.Equal("/Account/Login", response.Headers.Location?.OriginalString);
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.UserLogins.AnyAsync(x => x.UserId == user.Id));
        var profile = await client.GetAsync("/Profile");
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", profile.Headers.Location?.OriginalString);
    }

    private HttpClient ExternalClient(string email, string key, bool verified)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ExternalScheme);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, key),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "Google Test"),
            new Claim("google:email_verified", verified ? "true" : "false")
        }, "Google");
        var properties = new AuthenticationProperties();
        properties.Items["LoginProvider"] = "Google";
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, IdentityConstants.ExternalScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        return client;
    }
}

public sealed class GoogleOAuthConfigurationTests
{
    [Fact]
    public async Task Configured_google_provider_is_visible_and_starts_oauth_challenge()
    {
        using var factory = new GoogleConfiguredFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var login = await client.GetAsync("/Account/Login");
        login.EnsureSuccessStatusCode();
        var html = await login.Content.ReadAsStringAsync();
        Assert.Contains("Войти через Google", html);

        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        var response = await client.PostAsync("/Account/ExternalLogin", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["provider"] = "Google",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("accounts.google.com", response.Headers.Location?.Host);
        Assert.Contains("/signin-google", Uri.UnescapeDataString(response.Headers.Location?.Query ?? ""));
        Assert.Contains(response.Headers, header =>
            header.Key == "Set-Cookie" && header.Value.Any(value => value.Contains(".AspNetCore.Correlation.", StringComparison.Ordinal)));
    }

    private sealed class GoogleConfiguredFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"FitMindGoogleTests_{Guid.NewGuid():N}";
        private bool databaseDeleted;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id.apps.googleusercontent.com");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DemoAccount:Enabled"] = "false",
                    ["Authentication:Google:ClientId"] = "test-client-id.apps.googleusercontent.com",
                    ["Authentication:Google:ClientSecret"] = "test-client-secret"
                }));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !databaseDeleted)
            {
                databaseDeleted = true;
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (!string.Equals(db.Database.GetDbConnection().Database, databaseName, StringComparison.Ordinal))
                    throw new InvalidOperationException("Google OAuth test attempted to clean a database outside its isolated scope.");
                db.Database.EnsureDeleted();
            }
            base.Dispose(disposing);
        }
    }
}
