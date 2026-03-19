using Lotplapp.Features.Plots.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Lotplapp.Features.Plots.Presentation;

public partial class EditPlot
{
    [Parameter]
    public int Id { get; set; }

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
    private bool _notFound;
    private bool _isAdmin;
    private List<(string Id, string FullName)> _ownerUsers = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var currentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        _isAdmin = user.IsInRole("Admin");

        var plot = await PlotRepository.GetByIdAsync(Id);
        if (plot is null)
        {
            _notFound = true;
            return;
        }

        // Owner trying to edit another owner's plot → show access denied UI
        if (!_isAdmin && plot.OwnerId != currentUserId)
        {
            _notFound = true;
            return;
        }

        _name = plot.Name;
        _ownerId = plot.OwnerId;
        _currency = plot.Currency;

        if (_isAdmin)
        {
            _ownerUsers = await PlotRepository.GetOwnerUsersAsync();
        }
    }

    private async Task HandleSubmitAsync()
    {
        _errors = [];

        if (_notFound) return;

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
                Id = Id,
                Name = _name.Trim(),
                OwnerId = _ownerId,
                Currency = _currency.Trim(),
            };

            var success = await PlotRepository.UpdateAsync(plot);
            if (success)
            {
                NavigationManager.NavigateTo("/plots");
            }
            else
            {
                _errors.Add("Failed to update plot. It may have been deleted.");
            }
        }
        finally
        {
            _isLoading = false;
        }
    }
}
