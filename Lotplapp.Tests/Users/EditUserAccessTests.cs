using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Lotplapp.Tests.Auth;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Integration tests for SPEC-8.1 access control on the edit page.
/// Verifies that a Reporter (authenticated but not Admin/Owner) is redirected
/// to /auth/access-denied when attempting to access /users/edit/{id}.
/// </summary>
public class EditUserAccessTests : IClassFixture<LoginWebAppFactory>
{
    private readonly LoginWebAppFactory _factory;

    public EditUserAccessTests(LoginWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// SPEC-8.1 Scenario: Non-Admin/Owner attempts to access edit page.
    /// GIVEN an authenticated Reporter
    /// WHEN they navigate to /users/edit/{id}
    /// THEN the system returns a redirect to /auth/access-denied (302)
    /// </summary>
    [Fact]
    public async Task Reporter_CannotAccess_EditUserPage_RedirectsToAccessDenied()
    {
        // Arrange
        await EnsureReporterUserAsync("reporter-edit-access@test.com");

        var client = _factory.CreateClientWithoutAutoRedirect();

        // Login as Reporter
        var token = await FetchAntiforgeryTokenAsync(client);
        var loginResponse = await client.PostAsync(
            "/auth/login",
            LoginForm("reporter-edit-access@test.com", "Test@1234", token),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // Act — Reporter attempts to access the edit page
        var response = await client.GetAsync(
            "/users/edit/00000000-0000-0000-0000-000000000001",
            TestContext.Current.CancellationToken);

        // Assert — must redirect to /auth/access-denied (not /auth/login)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/access-denied", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task EnsureReporterUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        if (!await roleManager.RoleExistsAsync(UserRoles.Reporter))
            await roleManager.CreateAsync(new IdentityRole(UserRoles.Reporter));

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new User
            {
                FullName = "Reporter Edit Access",
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
            };
            var result = await userManager.CreateAsync(user, "Test@1234");
            if (!result.Succeeded)
                throw new InvalidOperationException("Failed to seed reporter user.");
            await userManager.AddToRoleAsync(user, UserRoles.Reporter);
        }
        else if (!await userManager.IsInRoleAsync(existing, UserRoles.Reporter))
        {
            await userManager.AddToRoleAsync(existing, UserRoles.Reporter);
        }
    }

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
