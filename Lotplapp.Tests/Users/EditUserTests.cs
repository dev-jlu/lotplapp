using System.Threading.Tasks;
using Lotplapp.Features.Users.Domain;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for the EditUser page logic (SPEC-8.1).
/// Uses <see cref="EditUserTestHarness"/> which replicates the HandleSubmit and
/// OnInitializedAsync logic from EditUser.razor.cs without needing a Blazor pipeline.
/// Follows the same pattern as <see cref="CreateUserTestHarness"/> / CreateUserRoleGuardTests.
/// </summary>
public class EditUserTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Mock<IUserRepository> BuildRepoMock(
        User? userToReturn = null,
        bool updateSuccess = true,
        IEnumerable<string>? updateErrors = null)
    {
        var mock = new Mock<IUserRepository>();

        mock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(userToReturn);

        mock.Setup(r => r.GetUserRolesAsync(It.IsAny<List<User>>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [userToReturn?.Id ?? "x"] = "Seller"
            });

        mock.Setup(r => r.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((updateSuccess, updateErrors ?? []));

        return mock;
    }

    // ---------------------------------------------------------------------------
    // Validation — empty FullName
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleSubmit_EmptyFullName_AddsValidationErrorAndDoesNotCallUpdate()
    {
        // Arrange
        var user = new User { Id = "u1", FullName = "Alice", Email = "alice@test.com" };
        var repoMock = BuildRepoMock(userToReturn: user);
        var sut = new EditUserTestHarness(repoMock.Object, "u1");
        await sut.InitializeAsync();

        sut.SetFormValues(fullName: "", email: "alice@test.com", role: "Seller");

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert
        Assert.Contains("Full name is required.", sut.Errors);
        repoMock.Verify(r => r.UpdateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Validation — empty Email
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleSubmit_EmptyEmail_AddsValidationErrorAndDoesNotCallUpdate()
    {
        // Arrange
        var user = new User { Id = "u2", FullName = "Bob", Email = "bob@test.com" };
        var repoMock = BuildRepoMock(userToReturn: user);
        var sut = new EditUserTestHarness(repoMock.Object, "u2");
        await sut.InitializeAsync();

        sut.SetFormValues(fullName: "Bob", email: "", role: "Owner");

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert
        Assert.Contains("Email is required.", sut.Errors);
        repoMock.Verify(r => r.UpdateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Success path — calls UpdateAsync and navigates to /users
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleSubmit_ValidInput_CallsUpdateAsyncAndNavigatesToUsers()
    {
        // Arrange
        var user = new User { Id = "u3", FullName = "Carol", Email = "carol@test.com" };
        var repoMock = BuildRepoMock(userToReturn: user, updateSuccess: true);
        var sut = new EditUserTestHarness(repoMock.Object, "u3");
        await sut.InitializeAsync();

        sut.SetFormValues(fullName: "Carol Updated", email: "carol2@test.com", role: "Owner");

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert
        Assert.Empty(sut.Errors);
        Assert.True(sut.NavigatedToUsers);
        repoMock.Verify(r => r.UpdateAsync("u3", "Carol Updated", "carol2@test.com", "Owner"), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Failure path — UpdateAsync returns errors, no navigation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleSubmit_UpdateAsyncFails_DisplaysErrorsWithoutNavigation()
    {
        // Arrange
        var user = new User { Id = "u4", FullName = "Dave", Email = "dave@test.com" };
        var repoMock = BuildRepoMock(
            userToReturn: user,
            updateSuccess: false,
            updateErrors: ["Email 'taken@test.com' is already taken."]);

        var sut = new EditUserTestHarness(repoMock.Object, "u4");
        await sut.InitializeAsync();

        sut.SetFormValues(fullName: "Dave", email: "taken@test.com", role: "Seller");

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert
        Assert.Contains("Email 'taken@test.com' is already taken.", sut.Errors);
        Assert.False(sut.NavigatedToUsers);
    }

    // ---------------------------------------------------------------------------
    // User not found — displays not-found, no Update call
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleSubmit_UserNotFound_DisplaysNotFoundError()
    {
        // Arrange — GetByIdAsync returns null
        var repoMock = BuildRepoMock(userToReturn: null);
        var sut = new EditUserTestHarness(repoMock.Object, "ghost");
        await sut.InitializeAsync();

        sut.SetFormValues(fullName: "Ghost", email: "ghost@test.com", role: "Seller");

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert
        Assert.True(sut.NotFound);
        repoMock.Verify(r => r.UpdateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}

/// <summary>
/// Test harness that replicates the logic of EditUser.razor.cs
/// (OnInitializedAsync + HandleSubmit) without a Blazor rendering pipeline.
/// Mirrors the <see cref="CreateUserTestHarness"/> pattern.
/// </summary>
public class EditUserTestHarness
{
    private readonly IUserRepository _userRepository;
    private readonly string _id;

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public List<string> Errors { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public bool NotFound { get; private set; }
    public bool NavigatedToUsers { get; private set; }

    public EditUserTestHarness(IUserRepository userRepository, string id)
    {
        _userRepository = userRepository;
        _id = id;
    }

    public void SetFormValues(string fullName, string email, string role)
    {
        FullName = fullName;
        Email = email;
        Role = role;
    }

    /// <summary>
    /// Replicates OnInitializedAsync: load user + pre-fill role.
    /// Sets NotFound if user is null.
    /// </summary>
    public async Task InitializeAsync()
    {
        var user = await _userRepository.GetByIdAsync(_id);
        if (user is null)
        {
            NotFound = true;
            return;
        }

        // Pre-fill form
        FullName = user.FullName;
        Email = user.Email ?? string.Empty;

        var roles = await _userRepository.GetUserRolesAsync([user]);
        Role = roles.GetValueOrDefault(user.Id, string.Empty);
    }

    /// <summary>
    /// Replicates HandleSubmit: validate → UpdateAsync → navigate or display errors.
    /// Aborts immediately if NotFound (user wasn't loaded).
    /// </summary>
    public async Task InvokeHandleSubmitAsync()
    {
        Errors = [];

        // Guard: cannot submit if user was not found on load
        if (NotFound) return;

        if (string.IsNullOrWhiteSpace(FullName)) Errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(Email)) Errors.Add("Email is required.");

        if (Errors.Count != 0) return;

        IsLoading = true;

        var (success, errors) = await _userRepository.UpdateAsync(_id, FullName, Email, Role);

        if (success)
        {
            NavigatedToUsers = true; // simulates NavigationManager.NavigateTo("/users")
        }
        else
        {
            Errors = [.. errors];
        }

        IsLoading = false;
    }
}
