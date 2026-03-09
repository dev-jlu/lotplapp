using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Identity;
using static System.Net.Mime.MediaTypeNames;

namespace Lotplapp.Shared.Infrastructure.Persistence.Seeders;

public class AdminSeeder
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(UserManager<User> userManager, IConfiguration configuration, ILogger<AdminSeeder> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var adminEmail = _configuration["Seed:AdminEmail"];
        var adminPassword = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("Seed:AdminEmail or Seed:AdminPassword not configured. Skipping.");
            return;
        }

        var existing = await _userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            _logger.LogInformation("Admin account already exists. Skipping.");
            return;
        }

        var admin = new User
        {
            FullName = "Administrator",
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(admin, UserRoles.Admin);
            _logger.LogInformation("Admin accoutn '{Email}' seeded successfully.", adminEmail);
        }
        else
        {
            _logger.LogError("Failed to seed admin: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
