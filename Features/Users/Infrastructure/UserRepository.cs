using Lotplapp.Features.Users.Domain;
using Lotplapp.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lotplapp.Features.Users.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _dbContext;

    public UserRepository(UserManager<User> userManager, AppDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> CreateAsync(
        string fullName,
        string email,
        string password,
        string role)
    {
        var user = new User
        {
            FullName = fullName,
            UserName = email,
            Email = email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description));

        await _userManager.AddToRoleAsync(user, role);
        return (true, []);
    }

    public async Task<Dictionary<string, string>> GetUserRolesAsync(List<User> users)
    {
        var userIds = users.Select(u => u.Id).ToList();

        var userRoles = await _dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync();

        var roleIds = userRoles.Select(r => r.RoleId).Distinct().ToList();

        var roles = await _dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? "-");

        return userRoles.ToDictionary(
            ur => ur.UserId,
            ur => roles.GetValueOrDefault(ur.RoleId, "-")
        );
    }

    public async Task<User?> GetByIdAsync(string id)
        => await _userManager.FindByIdAsync(id);

    public async Task<bool> SetActiveStatusAsync(string id, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return false;

        user.IsActive = isActive;
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> UpdateAsync(
        string id,
        string fullName,
        string email,
        string role)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return (false, ["User not found."]);

        user.FullName = fullName;
        user.UserName = email;
        user.Email = email;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return (false, updateResult.Errors.Select(e => e.Description));

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        return (true, []);
    }
}
