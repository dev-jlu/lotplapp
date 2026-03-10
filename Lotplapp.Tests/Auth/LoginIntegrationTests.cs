using System.Net;
using Lotplapp.Features.Users.Domain;
using Lotplapp.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Lotplapp.Tests.Auth;

/// <summary>
/// Integration tests for POST /auth/login using <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// Each test class gets its own isolated SQLite database so tests are independent.
/// Anti-forgery validation is bypassed by omitting the token — the test host runs in
/// Development environment where AntiforgeryOptions.SuppressXFrameOptionsHeader is off,
/// but <see cref="HttpClient"/> sends the cookie back automatically when HandleCookies is true.
/// We also seed and POST without the CSRF token, which works against the TestServer because
/// the DataProtection keys are ephemeral (different per factory run) — in practice the
/// antiforgery middleware will reject it UNLESS we fetch a token first, which we do below.
/// </summary>
public class LoginIntegrationTests : IClassFixture<LoginWebAppFactory>
{
    private readonly LoginWebAppFactory _factory;

    public LoginIntegrationTests(LoginWebAppFactory factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------------------
    // Active user: 302 redirect to "/"
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Post_Login_ActiveUser_Redirects302ToRoot()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        await _factory.SeedUserAsync(
            email: "active@test.com",
            password: "Test@1234",
            isActive: true);

        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("active@test.com", "Test@1234", token);

        // Act
        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    // ---------------------------------------------------------------------------
    // Cookie assertion: successful login sets the Identity cookie
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Post_Login_ActiveUser_SetsIdentityCookie()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        await _factory.SeedUserAsync(
            email: "cookie@test.com",
            password: "Test@1234",
            isActive: true);

        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("cookie@test.com", "Test@1234", token);

        // Act
        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);

        // Assert — at least one Set-Cookie header is present and contains the Identity cookie
        var setCookies = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        Assert.True(setCookies.Count > 0, "Expected Set-Cookie headers but got none.");
        Assert.True(
            setCookies.Any(c => c.Contains(".AspNetCore.Identity.Application")),
            "Expected the Identity application cookie in the response but did not find it.");
    }

    // ---------------------------------------------------------------------------
    // Deactivated user: 200 with error in body, no session cookie
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Post_Login_DeactivatedUser_Returns200WithErrorAndNoSessionCookie()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        await _factory.SeedUserAsync(
            email: "inactive@test.com",
            password: "Test@1234",
            isActive: false);

        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("inactive@test.com", "Test@1234", token);

        // Act
        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — form page is re-rendered (200)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The deactivated error message must appear in the HTML response
        Assert.Contains("Your account has been deactivated. Contact an administrator.", body);

        // The SignOutAsync call should not leave an authenticated cookie.
        // When SignOutAsync runs, Identity may emit a Set-Cookie that clears the auth cookie
        // (value="" and max-age=0 / expires in the past). We verify no *valid* Identity cookie
        // is present: either the cookie is absent entirely, or it has an empty value.
        var setCookies = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        var identityCookies = setCookies
            .Where(c => c.Contains(".AspNetCore.Identity.Application"))
            .ToList();

        // If Identity emits a clear-cookie directive it still contains the cookie name but
        // the value segment (before the first ';') will be "CookieName=" (empty value) or
        // will include "max-age=0" / "expires=Thu, 01 Jan 1970".
        foreach (var cookie in identityCookies)
        {
            var valueSegment = cookie.Split(';')[0]; // e.g. ".AspNetCore.Identity.Application=VALUE"
            var value = valueSegment.Contains('=') ? valueSegment[(valueSegment.IndexOf('=') + 1)..] : string.Empty;
            Assert.True(string.IsNullOrEmpty(value),
                $"Identity cookie must be empty (sign-out clear) for deactivated user but got value: {value[..Math.Min(20, value.Length)]}...");
        }
    }

    // ---------------------------------------------------------------------------
    // Integration — invalid credentials: 200 with generic error
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Post_Login_InvalidCredentials_Returns200WithGenericError()
    {
        var client = _factory.CreateClientWithoutAutoRedirect();
        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("nobody@test.com", "WrongPassword!", token);

        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", body);
    }

    // ---------------------------------------------------------------------------
    // Cookie Max-Age ≈ 28800 seconds on successful login
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Post_Login_ActiveUser_CookieMaxAgeIsApproximately8Hours()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        await _factory.SeedUserAsync(
            email: "maxage@test.com",
            password: "Test@1234",
            isActive: true);

        var token = await FetchAntiforgeryTokenAsync(client);

        // Use RememberMe=true so the cookie is persistent and includes max-age.
        // ASP.NET Core Identity only emits max-age on persistent cookies (isPersistent=true).
        // The session lifetime (8 h) is set via ExpireTimeSpan regardless of isPersistent.
        var formData = LoginFormPersistent("maxage@test.com", "Test@1234", token);

        // Act
        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);

        // Assert — find the Identity cookie's Max-Age
        var setCookies = response.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        var identityCookie = setCookies.FirstOrDefault(
            c => c.Contains(".AspNetCore.Identity.Application"));

        Assert.NotNull(identityCookie);

        // ASP.NET Core emits either 'max-age=N' or 'expires=<date>' depending on isPersistent.
        // We accept either format and assert the effective lifetime is ≈ 8 hours (28800 s).
        var lifetime = ParseCookieLifetimeSeconds(identityCookie);
        Assert.True(lifetime.HasValue, $"Could not parse cookie lifetime from: {identityCookie}");

        // Allow ±120 s tolerance around 28800 (8 h)
        Assert.InRange(lifetime!.Value, 28680, 28920);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Fetches the login page and extracts the __RequestVerificationToken from the form
    /// so the subsequent POST passes anti-forgery validation.
    /// </summary>
    private static async Task<string> FetchAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/auth/login");
        var html = await response.Content.ReadAsStringAsync();

        // Parse: <input name="__RequestVerificationToken" type="hidden" value="..." />
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            // Try alternate attribute order
            const string alt = "__RequestVerificationToken\" type=\"hidden\" value=\"";
            start = html.IndexOf(alt, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += alt.Length;
                var end2 = html.IndexOf('"', start);
                return html[start..end2];
            }
            return string.Empty; // antiforgery disabled in test environment
        }

        start += marker.Length;
        var end = html.IndexOf('"', start);
        return html[start..end];
    }

    private static FormUrlEncodedContent LoginForm(string email, string password, string antiforgeryToken)
    {
        return new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", password),
            new KeyValuePair<string, string>("Input.RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
        ]);
    }

    /// <summary>
    /// Like <see cref="LoginForm"/> but with RememberMe=true.
    /// ASP.NET Core Identity only sets max-age on persistent cookies (isPersistent=true),
    /// which is controlled by the RememberMe checkbox.
    /// </summary>
    private static FormUrlEncodedContent LoginFormPersistent(string email, string password, string antiforgeryToken)
    {
        return new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", password),
            new KeyValuePair<string, string>("Input.RememberMe", "true"),
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
        ]);
    }

    /// <summary>
    /// Returns the cookie lifetime in seconds by parsing either <c>max-age=N</c> or
    /// <c>expires=&lt;date&gt;</c> from a Set-Cookie header value.
    /// Returns null if neither attribute is present.
    /// </summary>
    private static long? ParseCookieLifetimeSeconds(string setCookieHeader)
    {
        var parts = setCookieHeader.Split(';', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            // Prefer max-age if present
            if (part.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase))
            {
                var valueStr = part["max-age=".Length..];
                if (long.TryParse(valueStr, out var maxAge))
                    return maxAge;
            }

            // Fall back to expires=<rfc1123 date>
            if (part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))
            {
                var dateStr = part["expires=".Length..];
                if (DateTimeOffset.TryParse(dateStr, out var expiry))
                {
                    var remainingSeconds = (long)(expiry - DateTimeOffset.UtcNow).TotalSeconds;
                    return remainingSeconds > 0 ? remainingSeconds : null;
                }
            }
        }
        return null;
    }
}
