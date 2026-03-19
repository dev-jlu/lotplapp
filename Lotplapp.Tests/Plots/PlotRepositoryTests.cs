using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lotplapp.Features.Plots.Domain;
using Lotplapp.Features.Plots.Infrastructure;
using Lotplapp.Features.Users.Domain;
using Lotplapp.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Plots;

/// <summary>
/// Unit tests for PlotRepository using in-memory SQLite for DbContext
/// and Moq for UserManager — mirrors the UserRepositoryTests pattern.
///
/// Note: OwnerId is a FK to AspNetUsers.Id (ON DELETE RESTRICT).
/// Each test that inserts a Plot must first seed a User row with the
/// matching Id so the FK constraint is satisfied.
/// </summary>
public class PlotRepositoryTests : IDisposable
{
    // ---------------------------------------------------------------------------
    // Infrastructure helpers
    // ---------------------------------------------------------------------------

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static AppDbContext BuildInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=test-plots-{Guid.NewGuid():N}.db")
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>
    /// Inserts a minimal User row directly into AspNetUsers so that Plot FK constraints
    /// are satisfied when seeding test data. Does not go through Identity (no hashing).
    /// </summary>
    private async Task<string> SeedUserAsync(string id)
    {
        var user = new User
        {
            Id = id,
            UserName = $"{id}@test.local",
            NormalizedUserName = $"{id}@TEST.LOCAL",
            Email = $"{id}@test.local",
            NormalizedEmail = $"{id}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            FullName = $"Test User {id}",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return id;
    }

    private readonly AppDbContext _db;
    private readonly Mock<UserManager<User>> _userMgr;
    private readonly PlotRepository _sut;

    public PlotRepositoryTests()
    {
        _db = BuildInMemoryDbContext();
        _userMgr = BuildUserManagerMock();
        _sut = new PlotRepository(_db, _userMgr.Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ---------------------------------------------------------------------------
    // GetAllAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsAllPlots_RegardlessOfOwner()
    {
        // Arrange — two plots with different owner IDs
        var ownerA = await SeedUserAsync("owner-all-a");
        var ownerB = await SeedUserAsync("owner-all-b");

        _db.Plots.AddRange(
            new Plot { Name = "Plot from Owner A", OwnerId = ownerA, Currency = "MXN" },
            new Plot { Name = "Plot from Owner B", OwnerId = ownerB, Currency = "USD" }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert — both plots returned regardless of which owner they belong to
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.OwnerId == ownerA);
        Assert.Contains(result, p => p.OwnerId == ownerB);
    }

    // ---------------------------------------------------------------------------
    // GetByOwnerAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByOwnerAsync_ReturnsOnlyPlotsOwnedBySpecifiedUser()
    {
        // Arrange
        var ownerA = await SeedUserAsync("owner-a-id");
        var ownerB = await SeedUserAsync("owner-b-id");

        _db.Plots.AddRange(
            new Plot { Name = "Plot A1", OwnerId = ownerA, Currency = "MXN" },
            new Plot { Name = "Plot A2", OwnerId = ownerA, Currency = "MXN" },
            new Plot { Name = "Plot B1", OwnerId = ownerB, Currency = "MXN" }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByOwnerAsync(ownerA);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal(ownerA, p.OwnerId));
    }

    [Fact]
    public async Task GetByOwnerAsync_NoMatchingOwner_ReturnsEmptyList()
    {
        // Arrange
        var otherId = await SeedUserAsync("other-owner");
        _db.Plots.Add(new Plot { Name = "Someone's Plot", OwnerId = otherId, Currency = "MXN" });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByOwnerAsync("nonexistent-owner");

        // Assert
        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetByIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsPlot()
    {
        // Arrange
        var ownerId = await SeedUserAsync("owner-1");
        var plot = new Plot { Name = "Test Plot", OwnerId = ownerId, Currency = "MXN" };
        _db.Plots.Add(plot);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(plot.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Plot", result.Name);
        Assert.Equal(ownerId, result.OwnerId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange — no plots in DB

        // Act
        var result = await _sut.GetByIdAsync(9999);

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // SetActiveStatusAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveStatusAsync_ActivePlot_SetsIsActiveToFalseAndReturnsTrue()
    {
        // Arrange
        var ownerId = await SeedUserAsync("owner-1");
        var plot = new Plot { Name = "Active Plot", OwnerId = ownerId, Currency = "MXN", IsActive = true };
        _db.Plots.Add(plot);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.SetActiveStatusAsync(plot.Id, false);

        // Assert
        Assert.True(result);
        var refreshed = await _db.Plots.FindAsync(plot.Id);
        Assert.False(refreshed!.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_InactivePlot_SetsIsActiveToTrueAndReturnsTrue()
    {
        // Arrange
        var ownerId = await SeedUserAsync("owner-1");
        var plot = new Plot { Name = "Inactive Plot", OwnerId = ownerId, Currency = "MXN", IsActive = false };
        _db.Plots.Add(plot);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.SetActiveStatusAsync(plot.Id, true);

        // Assert
        Assert.True(result);
        var refreshed = await _db.Plots.FindAsync(plot.Id);
        Assert.True(refreshed!.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_NonExistentPlot_ReturnsFalse()
    {
        // Arrange — no plots in DB

        // Act
        var result = await _sut.SetActiveStatusAsync(9999, false);

        // Assert
        Assert.False(result);
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ValidPlot_PersistsNameOwnerIdAndCurrencyChanges()
    {
        // Arrange
        var owner1Id = await SeedUserAsync("owner-1");
        var owner2Id = await SeedUserAsync("owner-2");
        var plot = new Plot { Name = "Original", OwnerId = owner1Id, Currency = "MXN" };
        _db.Plots.Add(plot);
        await _db.SaveChangesAsync();

        // Act
        var updated = new Plot { Id = plot.Id, Name = "Renamed", OwnerId = owner2Id, Currency = "USD" };
        var result = await _sut.UpdateAsync(updated);

        // Assert
        Assert.True(result);
        var refreshed = await _db.Plots.FindAsync(plot.Id);
        Assert.Equal("Renamed", refreshed!.Name);
        Assert.Equal(owner2Id, refreshed.OwnerId);
        Assert.Equal("USD", refreshed.Currency);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentPlot_ReturnsFalse()
    {
        // Arrange — no plots in DB

        // Act
        var result = await _sut.UpdateAsync(new Plot { Id = 9999, Name = "Ghost", OwnerId = "x", Currency = "MXN" });

        // Assert
        Assert.False(result);
    }

    // ---------------------------------------------------------------------------
    // CreateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidPlot_PersistsAndSetsCreatedAt()
    {
        // Arrange
        var ownerId = await SeedUserAsync("owner-1");
        var before = DateTime.UtcNow.AddSeconds(-1);
        var plot = new Plot { Name = "New Plot", OwnerId = ownerId, Currency = "MXN" };

        // Act
        var result = await _sut.CreateAsync(plot);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.CreatedAt >= before);
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
        var stored = await _db.Plots.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("New Plot", stored.Name);
    }
}
