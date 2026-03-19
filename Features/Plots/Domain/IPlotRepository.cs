namespace Lotplapp.Features.Plots.Domain;

public interface IPlotRepository
{
    Task<List<Plot>> GetAllAsync();
    Task<List<Plot>> GetByOwnerAsync(string ownerId);
    Task<Plot?> GetByIdAsync(int id);
    Task<Plot> CreateAsync(Plot plot);
    Task<bool> UpdateAsync(Plot plot);
    Task<bool> SetActiveStatusAsync(int id, bool isActive);
    Task<Dictionary<string, string>> GetOwnerNamesAsync(List<Plot> plots);

    /// <summary>Returns all users in the Owner role as (Id, FullName) tuples — for the Admin dropdown.</summary>
    Task<List<(string Id, string FullName)>> GetOwnerUsersAsync();
}
