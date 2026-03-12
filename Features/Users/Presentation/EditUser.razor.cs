using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Components;

namespace Lotplapp.Features.Users.Presentation;

public partial class EditUser
{
    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Inject]
    private IUserRepository UserRepository { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _role = string.Empty;
    private List<string> _errors = [];
    private bool _isLoading;
    private bool _notFound;

    protected override async Task OnInitializedAsync()
    {
        var user = await UserRepository.GetByIdAsync(Id);
        if (user is null)
        {
            _notFound = true;
            return;
        }

        _fullName = user.FullName;
        _email = user.Email ?? string.Empty;

        var roles = await UserRepository.GetUserRolesAsync([user]);
        _role = roles.GetValueOrDefault(user.Id, string.Empty);
    }

    private async Task HandleSubmit()
    {
        _errors = [];

        if (_notFound) return;

        if (string.IsNullOrWhiteSpace(_fullName)) _errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(_email)) _errors.Add("Email is required.");

        if (_errors.Count != 0) return;

        _isLoading = true;
        try
        {
            var (success, errors) = await UserRepository.UpdateAsync(Id, _fullName, _email, _role);

            if (success)
            {
                NavigationManager.NavigateTo("/users");
            }
            else
            {
                _errors = [.. errors];
            }
        }
        finally
        {
            _isLoading = false;
        }
    }
}
