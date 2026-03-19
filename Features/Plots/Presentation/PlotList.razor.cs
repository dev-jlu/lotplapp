using Lotplapp.Features.Plots.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Lotplapp.Features.Plots.Presentation;

public partial class PlotList
{
    [Inject]
    private IPlotRepository PlotRepository { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private List<Plot>? _plots;
    private Dictionary<string, string> _ownerNames = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        bool isAdmin = user.IsInRole("Admin");

        _plots = isAdmin
            ? await PlotRepository.GetAllAsync()
            : await PlotRepository.GetByOwnerAsync(userId!);

        _ownerNames = await PlotRepository.GetOwnerNamesAsync(_plots);
    }

    private async Task HandleToggleActiveAsync(int plotId, bool isActive)
    {
        await PlotRepository.SetActiveStatusAsync(plotId, isActive);

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        bool isAdmin = user.IsInRole("Admin");

        _plots = isAdmin
            ? await PlotRepository.GetAllAsync()
            : await PlotRepository.GetByOwnerAsync(userId!);

        _ownerNames = await PlotRepository.GetOwnerNamesAsync(_plots);
        StateHasChanged();
    }
}
