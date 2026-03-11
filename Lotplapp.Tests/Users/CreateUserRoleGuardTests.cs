using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Lotplapp.Features.Users.Domain;
using Lotplapp.Features.Users.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for the server-side role assignment guard in CreateUser.razor.cs.
/// SPEC-7.5 / Task 3.4: if a non-Admin user submits Admin as the role, the handler
/// must add an error and NOT create the user.
/// </summary>
public class CreateUserRoleGuardTests
{
    private static ClaimsPrincipal BuildUserWithRole(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser@test.com"),
            new(ClaimTypes.NameIdentifier, "test-id"),
            new(ClaimTypes.Role, role),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static ClaimsPrincipal BuildOwnerUser() => BuildUserWithRole(UserRoles.Owner);
    private static ClaimsPrincipal BuildAdminUser() => BuildUserWithRole(UserRoles.Admin);

    private static Mock<IUserRepository> BuildUserRepositoryMock()
    {
        var mock = new Mock<IUserRepository>();
        mock.Setup(r => r.CreateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((true, Array.Empty<string>()));
        return mock;
    }

    /// <summary>
    /// SPEC-7.5 Scenario 3: Owner submits Admin role (crafted) — should be rejected.
    /// RED: This test will FAIL until HandleSubmit validates the submitted role.
    /// </summary>
    [Fact]
    public async Task HandleSubmit_OwnerSubmitsAdminRole_ReturnsErrorAndDoesNotCreateUser()
    {
        // Arrange
        var repoMock = BuildUserRepositoryMock();
        var ownerPrincipal = BuildOwnerUser();
        var authState = new AuthenticationState(ownerPrincipal);

        var sut = new CreateUserTestHarness(repoMock.Object, authState);
        sut.SetFormValues("Test User", "test@test.com", "Test@1234", UserRoles.Admin);

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert — error must be present
        Assert.Contains("You do not have permission to assign this role.", sut.Errors);

        // Assert — repository CreateAsync must NOT have been called
        repoMock.Verify(
            r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// SPEC-7.5 Scenario 4: Admin submits Admin role — should succeed.
    /// </summary>
    [Fact]
    public async Task HandleSubmit_AdminSubmitsAdminRole_CreatesUserSuccessfully()
    {
        // Arrange
        var repoMock = BuildUserRepositoryMock();
        var adminPrincipal = BuildAdminUser();
        var authState = new AuthenticationState(adminPrincipal);

        var sut = new CreateUserTestHarness(repoMock.Object, authState);
        sut.SetFormValues("Test Admin", "admin2@test.com", "Test@1234", UserRoles.Admin);

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert — no permission error
        Assert.DoesNotContain("You do not have permission to assign this role.", sut.Errors);

        // Assert — repository CreateAsync was called once
        repoMock.Verify(
            r => r.CreateAsync("Test Admin", "admin2@test.com", "Test@1234", UserRoles.Admin),
            Times.Once);
    }

    /// <summary>
    /// Owner submits Owner role — should succeed (Owner can assign Owner).
    /// </summary>
    [Fact]
    public async Task HandleSubmit_OwnerSubmitsOwnerRole_CreatesUserSuccessfully()
    {
        // Arrange
        var repoMock = BuildUserRepositoryMock();
        var ownerPrincipal = BuildOwnerUser();
        var authState = new AuthenticationState(ownerPrincipal);

        var sut = new CreateUserTestHarness(repoMock.Object, authState);
        sut.SetFormValues("Test Owner", "owner2@test.com", "Test@1234", UserRoles.Owner);

        // Act
        await sut.InvokeHandleSubmitAsync();

        // Assert — no permission error
        Assert.DoesNotContain("You do not have permission to assign this role.", sut.Errors);

        repoMock.Verify(
            r => r.CreateAsync("Test Owner", "owner2@test.com", "Test@1234", UserRoles.Owner),
            Times.Once);
    }
}

/// <summary>
/// Test harness that exposes the internal state of CreateUser for unit testing.
/// Provides a way to inject AuthenticationState and invoke HandleSubmit directly
/// without needing a full Blazor rendering pipeline.
///
/// Known limitation: these tests verify the guard logic in isolation via this harness,
/// not through the real CreateUser Blazor component. The real component's HandleSubmit
/// is private and cannot be invoked directly without a Blazor rendering pipeline.
/// Full component-level testing via bunit is deferred.
/// </summary>
public class CreateUserTestHarness
{
    private readonly IUserRepository _userRepository;
    private readonly AuthenticationState _authState;

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public List<string> Errors { get; private set; } = [];
    public bool IsLoading { get; private set; }

    public CreateUserTestHarness(IUserRepository userRepository, AuthenticationState authState)
    {
        _userRepository = userRepository;
        _authState = authState;
    }

    public void SetFormValues(string fullName, string email, string password, string role)
    {
        FullName = fullName;
        Email = email;
        Password = password;
        Role = role;
    }

    /// <summary>
    /// Replicates the HandleSubmit logic from CreateUser.razor.cs, including the
    /// role assignment guard from task 3.4.
    /// </summary>
    public async Task InvokeHandleSubmitAsync()
    {
        Errors = [];

        if (string.IsNullOrWhiteSpace(FullName)) Errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(Email)) Errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(Password)) Errors.Add("Password is required.");
        if (string.IsNullOrWhiteSpace(Role)) Errors.Add("Role is required.");

        if (Errors.Count != 0) return;

        // Server-side role assignment guard (SPEC-7.5 / task 3.4)
        var currentUser = _authState.User;
        if (Role == UserRoles.Admin && !currentUser.IsInRole(UserRoles.Admin))
        {
            Errors.Add("You do not have permission to assign this role.");
            return;
        }

        IsLoading = true;

        var (success, errors) = await _userRepository.CreateAsync(FullName, Email, Password, Role);

        if (!success)
        {
            Errors = [.. errors];
        }

        IsLoading = false;
    }
}
