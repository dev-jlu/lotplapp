using System.Security.Claims;
using Lotplapp.Features.Auth.Pages;
using Lotplapp.Features.Users.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lotplapp.Tests.Auth;

/// <summary>
/// Unit tests for <see cref="LoginModel.OnPostAsync"/>.
/// All Identity services are mocked so tests run without a database.
/// </summary>
public class LoginModelTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Mock<SignInManager<User>> BuildSignInManagerMock()
    {
        var userStoreMock = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
        var logger = new Mock<ILogger<SignInManager<User>>>();

        var signInManager = new Mock<SignInManager<User>>(
            userManagerMock.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            logger.Object,
            null!,
            null!);

        return signInManager;
    }

    private static Mock<UserManager<User>> BuildUserManagerMock()
    {
        var userStoreMock = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    /// <summary>
    /// Creates a LoginModel wired up with the given mocks and a minimal page context
    /// so that <see cref="ModelState"/> and <see cref="PageModel.Page()"/> work.
    /// </summary>
    private static LoginModel BuildLoginModel(
        Mock<SignInManager<User>> signInManagerMock,
        Mock<UserManager<User>> userManagerMock)
    {
        var logger = new Mock<ILogger<LoginModel>>();
        var model = new LoginModel(signInManagerMock.Object, userManagerMock.Object, logger.Object);

        // Attach a minimal ActionContext so ModelState and Page() are usable.
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        var pageActionDescriptor = new Microsoft.AspNetCore.Mvc.RazorPages.CompiledPageActionDescriptor();
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var modelState = new ModelStateDictionary();

        var actionContext = new ActionContext(httpContext, routeData, pageActionDescriptor, modelState);
        var pageContext = new PageContext(actionContext)
        {
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                modelMetadataProvider, modelState)
        };

        model.PageContext = pageContext;
        model.Url = new UrlHelper(actionContext);

        return model;
    }

    private static LoginModel.InputModel ValidInput(string email = "test@example.com", string password = "Password123") =>
        new() { Email = email, Password = password, RememberMe = false };

    // ---------------------------------------------------------------------------
    // Active account: redirect to "/"
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_ActiveUser_ReturnsLocalRedirectToRoot()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var activeUser = new User { Email = "test@example.com", IsActive = true };
        userMgrMock
            .Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(activeUser);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        var result = await model.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        // SignOutAsync must NOT have been called
        signInMock.Verify(s => s.SignOutAsync(), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Deactivated account: SignOut + ModelState error + PageResult
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_DeactivatedUser_SignsOutAndReturnsPageWithError()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        signInMock
            .Setup(s => s.SignOutAsync())
            .Returns(Task.CompletedTask);

        var inactiveUser = new User { Email = "test@example.com", IsActive = false };
        userMgrMock
            .Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(inactiveUser);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        var result = await model.OnPostAsync();

        // Assert — result is PageResult (form re-render, not a redirect)
        Assert.IsType<PageResult>(result);

        // SignOutAsync must have been called exactly once
        signInMock.Verify(s => s.SignOutAsync(), Times.Once);

        // ModelState must contain the exact deactivated error string
        Assert.False(model.ModelState.IsValid);
        var errors = model.ModelState[string.Empty]?.Errors;
        Assert.NotNull(errors);
        Assert.Contains(errors, e => e.ErrorMessage ==
            "Your account has been deactivated. Contact an administrator.");
    }

    // ---------------------------------------------------------------------------
    // Deactivated account error must be model-level (key = "")
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_DeactivatedUser_ErrorKeyIsModelLevel()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        signInMock.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

        var inactiveUser = new User { Email = "test@example.com", IsActive = false };
        userMgrMock
            .Setup(u => u.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(inactiveUser);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        await model.OnPostAsync();

        // Assert — error must be at key "" (model-level), NOT "Input.Email" / "Input.Password"
        Assert.True(model.ModelState.ContainsKey(string.Empty),
            "Expected error to be keyed at string.Empty (model-level), but it was not.");
        Assert.False(model.ModelState.ContainsKey("Input.Email"),
            "Error must not be field-level on Input.Email.");
        Assert.False(model.ModelState.ContainsKey("Input.Password"),
            "Error must not be field-level on Input.Password.");
    }

    // ---------------------------------------------------------------------------
    // Invalid credentials: FindByEmailAsync NOT called, correct error
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_InvalidCredentials_ReturnsPageWithErrorAndDoesNotCheckIsActive()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        var result = await model.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);

        // FindByEmailAsync must NOT have been called (guard should not run on failure path)
        userMgrMock.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);

        var errors = model.ModelState[string.Empty]?.Errors;
        Assert.NotNull(errors);
        Assert.Contains(errors, e => e.ErrorMessage == "Invalid email or password.");
    }

    // ---------------------------------------------------------------------------
    // Locked-out: FindByEmailAsync NOT called, correct lockout error
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_LockedOut_ReturnsPageWithLockoutErrorAndDoesNotCheckIsActive()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        var result = await model.OnPostAsync();

        // Assert
        Assert.IsType<PageResult>(result);

        // FindByEmailAsync must NOT have been called
        userMgrMock.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);

        var errors = model.ModelState[string.Empty]?.Errors;
        Assert.NotNull(errors);
        Assert.Contains(errors, e => e.ErrorMessage == "Account locked. Try again later.");
    }

    // ---------------------------------------------------------------------------
    // Null user from FindByEmailAsync on Succeeded path → redirect to "/"
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnPostAsync_NullUserAfterSucceeded_RedirectsToRootWithoutCrash()
    {
        // Arrange
        var signInMock = BuildSignInManagerMock();
        var userMgrMock = BuildUserManagerMock();

        signInMock
            .Setup(s => s.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // FindByEmailAsync returns null (edge case: user deleted between sign-in and lookup)
        userMgrMock
            .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var model = BuildLoginModel(signInMock, userMgrMock);
        model.Input = ValidInput();

        // Act
        var result = await model.OnPostAsync();

        // Assert — should redirect to "/" without crash (null-safe guard: user != null && !user.IsActive)
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        signInMock.Verify(s => s.SignOutAsync(), Times.Never);
    }
}
