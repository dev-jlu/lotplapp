using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
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
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the SQLite connection string
/// with an isolated in-process SQLite file per factory instance, creates the schema on init,
/// and neutralises the startup seeder so tests control all data.
/// </summary>
public class LoginWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = $"test-{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the production AppDbContext with an isolated SQLite test database.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));

            // Build a temporary service provider to run EnsureCreated before the startup
            // seeders in Program.cs execute. Without this the RoleSeeder runs against an
            // empty SQLite file and crashes with "no such table: AspNetRoles".
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Empty seed credentials → seeders skip gracefully, leaving the DB clean.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "",
                ["Seed:AdminPassword"] = "",
            });
        });
    }

    /// <summary>
    /// Schema is already created synchronously inside ConfigureWebHost before startup seeders run.
    /// This method is required by IAsyncLifetime but has nothing to do here.
    /// </summary>
    // xunit v3: IAsyncLifetime.InitializeAsync returns ValueTask (not Task)
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>Deletes the test database file after all tests in the class finish.</summary>
    // xunit v3: IAsyncLifetime.DisposeAsync returns ValueTask (not Task)
    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    /// <summary>
    /// Seeds a user into the isolated test database. Idempotent — skips if email already exists.
    /// </summary>
    public async Task SeedUserAsync(string email, string password, bool isActive)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return;

        var user = new User
        {
            FullName = "Test User",
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = isActive,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user '{email}': " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Returns an HttpClient configured to NOT follow redirects so tests can assert on raw 302
    /// responses, with cookie handling enabled so the antiforgery cookie round-trips correctly.
    /// </summary>
    public HttpClient CreateClientWithoutAutoRedirect() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
}