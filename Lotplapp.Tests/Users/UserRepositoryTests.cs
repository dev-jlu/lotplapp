using System.Collections.Generic;
using System.Threading.Tasks;
using Lotplapp.Features.Users.Domain;
using Lotplapp.Features.Users.Infrastructure;
using Lotplapp.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for the three new UserRepository methods added in SCRUM-8:
/// GetByIdAsync, UpdateAsync, SetActiveStatusAsync.
/// UserManager&lt;User&gt; is mocked via Moq following the pattern in LoginModelTests.cs.
/// </summary>
public class UserRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var userStoreMock = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    /// <summary>
    /// Builds a UserRepository with a mocked UserManager.
    /// AppDbContext is not needed for GetByIdAsync / SetActiveStatusAsync / UpdateAsync
    /// because those methods only use UserManager, not DbContext.
    /// We pass null for AppDbContext — tests that need it should use integration tests.
    /// </summary>
    private static UserRepository BuildRepository(Mock<UserManager<User>> userManagerMock) =>
        new UserRepository(userManagerMock.Object, null!);

    // ---------------------------------------------------------------------------
    // GetByIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUser()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        var expectedUser = new User { Id = "user-1", FullName = "Alice", Email = "alice@test.com" };

        userMgr.Setup(u => u.FindByIdAsync("user-1"))
               .ReturnsAsync(expectedUser);

        var repo = BuildRepository(userMgr);

        // Act
        var result = await repo.GetByIdAsync("user-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user-1", result.Id);
        Assert.Equal("Alice", result.FullName);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(u => u.FindByIdAsync("missing-id"))
               .ReturnsAsync((User?)null);

        var repo = BuildRepository(userMgr);

        // Act
        var result = await repo.GetByIdAsync("missing-id");

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // SetActiveStatusAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveStatusAsync_ExistingUser_SetsIsActiveAndReturnsTrue()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        var user = new User { Id = "user-2", IsActive = true };

        userMgr.Setup(u => u.FindByIdAsync("user-2")).ReturnsAsync(user);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<User>()))
               .ReturnsAsync(IdentityResult.Success);

        var repo = BuildRepository(userMgr);

        // Act
        var result = await repo.SetActiveStatusAsync("user-2", false);

        // Assert
        Assert.True(result);
        Assert.False(user.IsActive); // mutation applied to the object
        userMgr.Verify(u => u.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetActiveStatusAsync_ExistingUser_Reactivates_SetsIsActiveAndReturnsTrue()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        var user = new User { Id = "user-reactivate", IsActive = false };

        userMgr.Setup(u => u.FindByIdAsync("user-reactivate")).ReturnsAsync(user);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<User>()))
               .ReturnsAsync(IdentityResult.Success);

        var repo = BuildRepository(userMgr);

        // Act
        var result = await repo.SetActiveStatusAsync("user-reactivate", true);

        // Assert
        Assert.True(result);
        Assert.True(user.IsActive);
        userMgr.Verify(u => u.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetActiveStatusAsync_MissingUser_ReturnsFalse()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(u => u.FindByIdAsync("ghost-id")).ReturnsAsync((User?)null);

        var repo = BuildRepository(userMgr);

        // Act
        var result = await repo.SetActiveStatusAsync("ghost-id", false);

        // Assert
        Assert.False(result);
        userMgr.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesFieldsAndRole_ReturnsSuccess()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        var user = new User { Id = "user-3", FullName = "Old Name", Email = "old@test.com" };

        userMgr.Setup(u => u.FindByIdAsync("user-3")).ReturnsAsync(user);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<User>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.GetRolesAsync(user))
               .ReturnsAsync(new List<string> { "Seller" });
        userMgr.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
               .ReturnsAsync(IdentityResult.Success);
        userMgr.Setup(u => u.AddToRoleAsync(user, "Owner"))
               .ReturnsAsync(IdentityResult.Success);

        var repo = BuildRepository(userMgr);

        // Act
        var (success, errors) = await repo.UpdateAsync("user-3", "New Name", "new@test.com", "Owner");

        // Assert
        Assert.True(success);
        Assert.Empty(errors);
        Assert.Equal("New Name", user.FullName);
        Assert.Equal("new@test.com", user.Email);
        Assert.Equal("new@test.com", user.UserName);
        userMgr.Verify(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()), Times.Once);
        userMgr.Verify(u => u.AddToRoleAsync(user, "Owner"), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateEmail_ReturnsIdentityErrors()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        var user = new User { Id = "user-4", FullName = "Bob" };

        userMgr.Setup(u => u.FindByIdAsync("user-4")).ReturnsAsync(user);
        userMgr.Setup(u => u.UpdateAsync(It.IsAny<User>()))
               .ReturnsAsync(IdentityResult.Failed(
                   new IdentityError { Code = "DuplicateEmail", Description = "Email 'dup@test.com' is already taken." }));

        var repo = BuildRepository(userMgr);

        // Act
        var (success, errors) = await repo.UpdateAsync("user-4", "Bob", "dup@test.com", "Seller");

        // Assert
        Assert.False(success);
        Assert.Contains("Email 'dup@test.com' is already taken.", errors);

        // Role change must NOT happen if UpdateAsync failed
        userMgr.Verify(u => u.RemoveFromRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        userMgr.Verify(u => u.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_MissingUser_ReturnsFalseWithError()
    {
        // Arrange
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(u => u.FindByIdAsync("no-user")).ReturnsAsync((User?)null);

        var repo = BuildRepository(userMgr);

        // Act
        var (success, errors) = await repo.UpdateAsync("no-user", "Doesn't Matter", "dm@test.com", "Seller");

        // Assert
        Assert.False(success);
        Assert.Contains("User not found.", errors);
        userMgr.Verify(u => u.UpdateAsync(It.IsAny<User>()), Times.Never);
    }
}
