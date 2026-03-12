using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace Lotplapp.Features.Users.Presentation;

public partial class UserList
{
    [Inject]
    private IUserRepository UserRepository { get; set; } = default!;
    [Inject]
    private UserManager<User> UserManager { get; set; } = default!;

    private List<User>? _users;
    private Dictionary<string, string> _roles = [];

    private string _nameFilter = string.Empty;
    private string _roleFilter = string.Empty;   // "" = All
    private string _statusFilter = string.Empty; // "" = All, "active", "inactive"

    private IEnumerable<User> FilteredUsers =>
        (_users ?? [])
            .Where(u => string.IsNullOrEmpty(_nameFilter) ||
                        u.FullName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) ||
                        (u.Email ?? "").Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(u => string.IsNullOrEmpty(_roleFilter) ||
                        _roles.GetValueOrDefault(u.Id) == _roleFilter)
            .Where(u => string.IsNullOrEmpty(_statusFilter) ||
                        (_statusFilter == "active" && u.IsActive) ||
                        (_statusFilter == "inactive" && !u.IsActive));

    protected override async Task OnInitializedAsync()
    {
        _users = await UserRepository.GetAllAsync();
        _roles = await UserRepository.GetUserRolesAsync(_users);
    }

    private async Task HandleSetActiveAsync(string userId, bool isActive)
    {
        await UserRepository.SetActiveStatusAsync(userId, isActive);
        _users = await UserRepository.GetAllAsync();
        _roles = await UserRepository.GetUserRolesAsync(_users);
        StateHasChanged();
    }
}
