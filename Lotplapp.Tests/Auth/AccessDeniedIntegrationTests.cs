using System.Net;
using Xunit;

namespace Lotplapp.Tests.Auth;

/// <summary>
/// Integration tests for the AccessDenied Razor Page at /auth/access-denied.
/// Phase 2 tasks 2.1, 2.2, 2.3:
///   - AccessDenied.cshtml.cs returns HTTP 403 on GET
///   - AccessDenied.cshtml renders an access-denied message
///   - Program.cs AccessDeniedPath is /auth/access-denied
/// </summary>
public class AccessDeniedIntegrationTests : IClassFixture<LoginWebAppFactory>
{
    private readonly LoginWebAppFactory _factory;

    public AccessDeniedIntegrationTests(LoginWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Scenario 3 from SPEC-3: Unauthenticated GET /auth/access-denied renders with HTTP 403.
    /// [AllowAnonymous] on the page model means no redirect to login.
    /// </summary>
    [Fact]
    public async Task Get_AccessDenied_Unauthenticated_Returns403()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        // Act
        var response = await client.GetAsync("/auth/access-denied", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario 1 from SPEC-3: The page renders a user-readable access-denied message.
    /// </summary>
    [Fact]
    public async Task Get_AccessDenied_RendersAccessDeniedContent()
    {
        // Arrange
        var client = _factory.CreateClientWithoutAutoRedirect();

        // Act
        var response = await client.GetAsync("/auth/access-denied", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert — page body must mention "Access Denied" (case-insensitive)
        Assert.Contains("Access Denied", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies Program.cs AccessDeniedPath is /auth/access-denied:
    /// an authenticated-but-unauthorized request to a protected page should redirect there.
    /// We simulate this by logging in as a user who has no role granting access to /users,
    /// then GETting /users and asserting the 302 Location points to /auth/access-denied.
    ///
    /// Note: this test also validates task 2.3 (AccessDeniedPath change in Program.cs).
    /// </summary>
    [Fact]
    public async Task Get_ProtectedRoute_AuthenticatedUnauthorizedUser_RedirectsToAuthAccessDenied()
    {
        // Arrange — seed a Seller user (cannot access /users)
        const string email = "seller-access-denied@test.com";
        const string password = "Test@1234";

        await _factory.SeedUserAsync(email, password, isActive: true);

        // We need the user to have a role so they are authenticated and in a role
        // that doesn't have access to /users. We use SeedUserAsync (no role) which
        // means the user is authenticated but has no role — which still triggers
        // the AccessDenied path when hitting a Roles-protected Razor Pages route.
        // However, /users is a Blazor component handled by MapRazorComponents, not
        // MapRazorPages, so the redirect test is best verified via /auth/access-denied
        // directly (covered by the two tests above). For this test we verify the
        // AccessDeniedPath redirect from a Razor Pages-style forbidden response.
        //
        // Since /users is served by Blazor SSR (not a Razor Page), the cookie
        // middleware AccessDeniedPath redirect is triggered by the authorization
        // middleware on the Blazor endpoint. We test that the redirect location
        // contains /auth/access-denied (not /auth/login as it was before).

        var client = _factory.CreateClientWithoutAutoRedirect();

        // Log in
        var token = await FetchAntiforgeryTokenAsync(client);
        var loginForm = LoginForm(email, password, token);
        var loginResponse = await client.PostAsync("/auth/login", loginForm, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Now hit a protected route the user is not authorized for.
        // /users requires Admin,Owner — a user with no role will be forbidden.
        var response = await client.GetAsync("/users", TestContext.Current.CancellationToken);

        // Assert — should be 302 redirect to /auth/access-denied (not /auth/login)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/access-denied", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    // ---------------------------------------------------------------------------
    // Helpers (copied from LoginIntegrationTests to keep tests self-contained)
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
}
