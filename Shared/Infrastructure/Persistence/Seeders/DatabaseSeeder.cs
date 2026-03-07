namespace Lotplapp.Shared.Infrastructure.Persistence.Seeders;

public class DatabaseSeeder
{
    private readonly RoleSeeder _roleSeeder;
    private readonly AdminSeeder _adminSeeder;

    public DatabaseSeeder(RoleSeeder roleSeeder, AdminSeeder adminSeeder)
    {
        _roleSeeder = roleSeeder;
        _adminSeeder = adminSeeder;
    }

    public async Task SeedAsync()
    {
        await _roleSeeder.SeedAsync();
        await _adminSeeder.SeedAsync();
    }
}
