using Lotplapp.Features.Plots.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Lotplapp.Features.Plots.Presentation;

public partial class CreatePlot
{
    [Inject]
    private IPlotRepository PlotRepository { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private string _name = string.Empty;
    private string _ownerId = string.Empty;
    private string _currency = "MXN";
    private List<string> _errors = [];
    private bool _isLoading;
    private bool _isAdmin;
    private string _currentUserId = string.Empty;
    private List<(string Id, string FullName)> _ownerUsers = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _currentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        _isAdmin = user.IsInRole("Admin");

        if (_isAdmin)
        {
            _ownerUsers = await PlotRepository.GetOwnerUsersAsync();
        }
    }

    private async Task HandleSubmitAsync()
    {
        _errors = [];

        if (string.IsNullOrWhiteSpace(_name))
            _errors.Add("Plot name is required.");

        if (_isAdmin && string.IsNullOrWhiteSpace(_ownerId))
            _errors.Add("Owner is required.");

        if (string.IsNullOrWhiteSpace(_currency))
            _errors.Add("Currency is required.");

        if (_errors.Count != 0)
            return;

        _isLoading = true;
        try
        {
            var plot = new Plot
            {
                Name = _name.Trim(),
                OwnerId = _isAdmin ? _ownerId : _currentUserId,
                Currency = _currency.Trim(),
            };

            await PlotRepository.CreateAsync(plot);
            NavigationManager.NavigateTo("/plots");
        }
        finally
        {
            _isLoading = false;
        }
    }
}
