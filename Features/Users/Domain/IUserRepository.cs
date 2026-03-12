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

    /// <summary>Returns the user with the given ID, or null if not found.</summary>
    Task<User?> GetByIdAsync(string id);

    /// <summary>
    /// Updates FullName, Email, UserName, and Role for the given user.
    /// Returns (true, []) on success, or (false, errors) on failure.
    /// </summary>
    Task<(bool Success, IEnumerable<string> Errors)> UpdateAsync(
        string id,
        string fullName,
        string email,
        string role);

    /// <summary>
    /// Sets IsActive on the user and persists via UserManager.
    /// Returns false if the user is not found.
    /// </summary>
    Task<bool> SetActiveStatusAsync(string id, bool isActive);
}
