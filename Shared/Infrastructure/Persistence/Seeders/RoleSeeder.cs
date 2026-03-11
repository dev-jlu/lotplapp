using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Identity;

namespace Lotplapp.Shared.Infrastructure.Persistence.Seeders;

public class RoleSeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<RoleSeeder> _logger;

    public RoleSeeder(RoleManager<IdentityRole> roleManager, ILogger<RoleSeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        string[] roles = new[] { UserRoles.Admin, UserRoles.Owner, UserRoles.Seller, UserRoles.Reporter };

        foreach (var role in roles)
        {
            if (await _roleManager.RoleExistsAsync(role)) continue;

            var result = await _roleManager.CreateAsync(new IdentityRole(role));

            if (result.Succeeded)
            {
                _logger.LogInformation("Role '{Role}' created successfully.", role);
            }
            else
            {
                _logger.LogWarning("Failed to create role '{Role}': {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
