using Lotplapp.Features.Users.Domain;
using Lotplapp.Shared.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for Shared/Infrastructure/Persistence/Seeders/RoleSeeder.cs.
/// Task 1.2 — SPEC-2.1: Reporter must be included in the seeded roles.
/// SPEC-2.2: Seeder must be idempotent (skip if already exists).
/// </summary>
public class RoleSeederTests
{
    private readonly Mock<RoleManager<IdentityRole>> _roleManagerMock;
    private readonly Mock<ILogger<RoleSeeder>> _loggerMock;
    private readonly RoleSeeder _sut;

    public RoleSeederTests()
    {
        var storeMock = new Mock<IRoleStore<IdentityRole>>();
        _roleManagerMock = new Mock<RoleManager<IdentityRole>>(
            storeMock.Object, null!, null!, null!, null!);
        _loggerMock = new Mock<ILogger<RoleSeeder>>();
        _sut = new RoleSeeder(_roleManagerMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Task 1.2 — Verifies that RoleSeeder seeds the Reporter role on a fresh DB.
    /// SPEC-2.1 + SPEC-2.3.
    /// </summary>
    [Fact]
    public async Task SeedAsync_FreshDatabase_CreatesReporterRole()
    {
        // Arrange — no roles exist yet
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _roleManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _sut.SeedAsync();

        // Assert — CreateAsync must have been called with "Reporter"
        _roleManagerMock.Verify(
            m => m.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.Reporter)),
            Times.Once,
            "Expected RoleSeeder to create the 'Reporter' role.");
    }

    /// <summary>
    /// SPEC-2.2 — Seeder is idempotent: if Reporter already exists, CreateAsync is NOT called.
    /// </summary>
    [Fact]
    public async Task SeedAsync_ReporterAlreadyExists_SkipsCreation()
    {
        // Arrange — all roles already exist
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        await _sut.SeedAsync();

        // Assert — CreateAsync must NOT have been called for Reporter
        _roleManagerMock.Verify(
            m => m.CreateAsync(It.Is<IdentityRole>(r => r.Name == UserRoles.Reporter)),
            Times.Never,
            "Expected RoleSeeder to skip creation when 'Reporter' already exists.");
    }

    /// <summary>
    /// Verifies all four roles are seeded on a fresh database.
    /// </summary>
    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsAllFourRoles()
    {
        // Arrange
        _roleManagerMock
            .Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _roleManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _sut.SeedAsync();

        // Assert — CreateAsync called exactly 4 times (Admin, Owner, Seller, Reporter)
        _roleManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<IdentityRole>()),
            Times.Exactly(4),
            "Expected RoleSeeder to create exactly 4 roles.");
    }
}
