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

    protected override async Task OnInitializedAsync()
    {
        _users = await UserRepository.GetAllAsync();
        _roles = await UserRepository.GetUserRolesAsync(_users);
    }
}
