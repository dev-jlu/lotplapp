using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Lotplapp.Features.Users.Presentation;

public partial class CreateUser
{
    [Inject]
    private IUserRepository UserRepository { get; set; } = default!;
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _role = string.Empty;
    private List<string> _errors = [];
    private bool _isLoading = false;

    private async Task HandleSubmit()
    {
        _errors = [];

        if (string.IsNullOrWhiteSpace(_fullName)) _errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(_email)) _errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(_password)) _errors.Add("Password is required.");
        if (string.IsNullOrWhiteSpace(_role)) _errors.Add("Role is required.");

        if (_errors.Count != 0)
        {
            foreach (var _error in _errors)
            {
                Console.WriteLine(_error);
            }
            return;
        }

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var currentUser = authState.User;
        if (_role == UserRoles.Admin && !currentUser.IsInRole(UserRoles.Admin))
        {
            _errors.Add("You do not have permission to assign this role.");
            return;
        }

        _isLoading = true;

        var (success, errors) = await UserRepository.CreateAsync(_fullName, _email, _password, _role);

        if (success)
        {
            NavigationManager.NavigateTo("/users");
        }
        else
        {
            _errors = [.. errors];
        }
        _isLoading = false;
    }
}
