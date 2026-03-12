using System.Net;
using Lotplapp.Tests.Auth;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Integration tests for SPEC-8.2: deactivated users must not be able to log in.
/// Uses <see cref="LoginWebAppFactory"/> (isolated SQLite per factory instance).
///
/// Note: The core deactivated-login check is also covered by
/// LoginIntegrationTests.Post_Login_DeactivatedUser_Returns200WithErrorAndNoSessionCookie.
/// This file verifies the SPEC-8.2 acceptance scenario — specifically that the response
/// body contains the word "deactivated" when a user with IsActive=false attempts login.
/// </summary>
public class DeactivatedUserLoginTests : IClassFixture<LoginWebAppFactory>
{
    private readonly LoginWebAppFactory _factory;

    public DeactivatedUserLoginTests(LoginWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// SPEC-8.2 Scenario: Deactivated user attempts to log in.
    /// GIVEN a user whose IsActive is false
    /// WHEN they submit valid credentials
    /// THEN login is rejected and the response body contains "deactivated"
    /// AND the user is NOT authenticated (no valid Identity cookie).
    /// </summary>
    [Fact]
    public async Task DeactivatedUser_CannotLogin_SeesDeactivatedMessage()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        await _factory.SeedUserAsync(
            email: "deactivated-scrum8@test.com",
            password: "Test@1234",
            isActive: false);

        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("deactivated-scrum8@test.com", "Test@1234", token);

        // Act
        var response = await client.PostAsync(
            "/auth/login", formData, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — the page is re-rendered (not redirected)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The body must contain the word "deactivated" (case-insensitive)
        Assert.Contains("deactivated", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------
    // Helpers (same pattern as LoginIntegrationTests)
    // ---------------------------------------------------------------------------

    private static async Task<string> FetchAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/auth/login");
        var html = await response.Content.ReadAsStringAsync();

        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            const string alt = "__RequestVerificationToken\" type=\"hidden\" value=\"";
            start = html.IndexOf(alt, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += alt.Length;
                var end2 = html.IndexOf('"', start);
                return html[start..end2];
            }
            return string.Empty;
        }

        start += marker.Length;
        var end = html.IndexOf('"', start);
        return html[start..end];
    }

    private static FormUrlEncodedContent LoginForm(string email, string password, string antiforgeryToken) =>
        new(
        [
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", password),
            new KeyValuePair<string, string>("Input.RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
        ]);
}
