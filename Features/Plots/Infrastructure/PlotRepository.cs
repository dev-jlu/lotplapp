using Lotplapp.Features.Plots.Domain;
using Lotplapp.Features.Users.Domain;
using Lotplapp.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lotplapp.Features.Plots.Infrastructure;

public class PlotRepository : IPlotRepository
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public PlotRepository(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<Plot>> GetAllAsync()
        => await _db.Plots.OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<List<Plot>> GetByOwnerAsync(string ownerId)
        => await _db.Plots
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Plot?> GetByIdAsync(int id)
        => await _db.Plots.FindAsync(id);

    public async Task<Plot> CreateAsync(Plot plot)
    {
        plot.CreatedAt = DateTime.UtcNow;
        _db.Plots.Add(plot);
        await _db.SaveChangesAsync();
        return plot;
    }

    public async Task<bool> UpdateAsync(Plot plot)
    {
        var existing = await _db.Plots.FindAsync(plot.Id);
        if (existing is null) return false;

        existing.Name = plot.Name;
        existing.OwnerId = plot.OwnerId;
        existing.Currency = plot.Currency;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveStatusAsync(int id, bool isActive)
    {
        var plot = await _db.Plots.FindAsync(id);
        if (plot is null) return false;

        plot.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Resolves owner display names for the given plots via a batch UserManager lookup.
    /// Returns a dictionary keyed by userId → FullName (or email as fallback).
    /// </summary>
    public async Task<Dictionary<string, string>> GetOwnerNamesAsync(List<Plot> plots)
    {
        var ownerIds = plots.Select(p => p.OwnerId).Distinct().ToList();
        var result = new Dictionary<string, string>();

        foreach (var ownerId in ownerIds)
        {
            var user = await _userManager.FindByIdAsync(ownerId);
            result[ownerId] = user?.FullName ?? user?.Email ?? ownerId;
        }

        return result;
    }

    public async Task<List<(string Id, string FullName)>> GetOwnerUsersAsync()
    {
        var owners = await _userManager.GetUsersInRoleAsync("Owner");
        return owners
            .OrderBy(u => u.FullName)
            .Select(u => (u.Id, u.FullName))
            .ToList();
    }
}
