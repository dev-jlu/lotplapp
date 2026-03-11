using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Collections.Generic;

namespace Lotplapp.Tests.Users;

public class ReporterAccessTests : IClassFixture<Lotplapp.Tests.Auth.LoginWebAppFactory>
{
    private readonly Lotplapp.Tests.Auth.LoginWebAppFactory _factory;

    public ReporterAccessTests(Lotplapp.Tests.Auth.LoginWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Spec: Reporter MUST NOT see the "+ Create User" link on the User Management page.
    /// RED: fails until UserList.razor wraps the link in AuthorizeView Roles="Admin,Owner".
    /// </summary>
    [Fact]
    public async Task Reporter_CannotSee_CreateUserLink()
    {
        var client = _factory.CreateClient();

        await EnsureReporterUserAsync();

        var token = await FetchAntiforgeryTokenAsync(client);
        await client.PostAsync("/auth/login", LoginForm("reporter@test.com", "Test@1234", token), TestContext.Current.CancellationToken);

        var userList = await client.GetAsync("/users", TestContext.Current.CancellationToken);
        var body = await userList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("href=\"/users/create\"", body);
    }

    [Fact]
    public async Task Reporter_CanAccess_ReadOnly_UserListPage()
    {
        var client = _factory.CreateClient();

        await EnsureReporterUserAsync();

        // Perform login via the auth flow
        var token = await FetchAntiforgeryTokenAsync(client);
        var formData = LoginForm("reporter@test.com", "Test@1234", token);

        var response = await client.PostAsync("/auth/login", formData, TestContext.Current.CancellationToken);

        // After following redirects, login should complete (200 or redirect final page); ensure cookie present implicitly.

        // Now request the user list page (client follows redirects by default)
        var userList = await client.GetAsync("/users", TestContext.Current.CancellationToken);

        var body = await userList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Expect the UserList page to render and contain "User Management"
        Assert.Contains("User Management", body);
    }

    private async Task EnsureReporterUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Lotplapp.Features.Users.Domain.User>>();

        if (!await roleManager.RoleExistsAsync(UserRoles.Reporter))
            await roleManager.CreateAsync(new IdentityRole(UserRoles.Reporter));

        const string email = "reporter@test.com";
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new Lotplapp.Features.Users.Domain.User
            {
                FullName = "Reporter User",
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
            };
            var res = await userManager.CreateAsync(user, "Test@1234");
            if (!res.Succeeded) throw new InvalidOperationException("Failed to seed reporter user.");
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
        var start = html.IndexOf(marker, System.StringComparison.Ordinal);
        if (start < 0)
        {
            const string alt = "__RequestVerificationToken\" type=\"hidden\" value=\"";
            start = html.IndexOf(alt, System.StringComparison.Ordinal);
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
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Input.Email", email),
                new KeyValuePair<string, string>("Input.Password", password),
                new KeyValuePair<string, string>("Input.RememberMe", "false"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
            });
    }
}
