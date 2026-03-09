namespace Lotplapp.Features.Users.Domain;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<(bool Success, IEnumerable<string> Errors)> CreateAsync(
        string fullName,
        string email,
        string password,
        string role);
    Task<Dictionary<string, string>> GetUserRolesAsync(List<User> users);
}
