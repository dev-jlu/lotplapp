---
name: tdd
description: >
  Test-Driven Development skill for the Lotplapp project using xUnit v3, Moq, and
  WebApplicationFactory. Guides the RED → GREEN → REFACTOR cycle with C#-specific patterns.
  Trigger: Loaded automatically by sdd-apply when TDD mode is detected.
license: Apache-2.0
metadata:
  author: dev-jlu
  version: "1.0"
---

## TDD Cycle

```
RED   → Write a failing test that describes the expected behavior
GREEN → Write the minimum code to make it pass
REFACTOR → Clean up without changing behavior, run tests again
```

**Never skip RED.** A test that passes before you write the implementation is either wrong or testing already-existing behavior.

---

## Test Runner

```bash
# Run a specific test class
dotnet test --filter "FullyQualifiedName~LoginModelTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~OnPostAsync_DeactivatedUser_SignsOutAndReturnsPageWithError"

# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

Always run **only the relevant test file/class** during the RED→GREEN cycle. Run the full suite only at REFACTOR time.

---

## Test Naming

```
MethodName_Scenario_ExpectedBehavior
```

| Example | Reads as |
|---|---|
| `OnPostAsync_DeactivatedUser_SignsOutAndReturnsPageWithError` | When OnPostAsync is called with a deactivated user, it signs out and returns the page with an error |
| `GetAllAsync_NoUsers_ReturnsEmptyList` | When GetAllAsync is called with no users, it returns an empty list |
| `CreateAsync_DuplicateEmail_ReturnsFalseWithErrors` | When CreateAsync is called with a duplicate email, it returns false with errors |

---

## Test Project Structure

Mirror the feature path from the main project:

```
Lotplapp.Tests/
├── Auth/
│   ├── LoginModelTests.cs         ← unit tests for Features/Auth/Pages/Login.cshtml.cs
│   ├── LoginIntegrationTests.cs   ← integration tests for /auth/login endpoint
│   └── LoginWebAppFactory.cs      ← custom factory for auth integration tests
└── Users/
    ├── UserRepositoryTests.cs     ← unit tests for Features/Users/Infrastructure/UserRepository.cs
    └── UsersIntegrationTests.cs   ← integration tests for /users endpoints
```

---

## Unit Tests (Moq)

Use for: PageModel handlers, repository methods, service logic — anything with injectable dependencies.

```csharp
public class UserRepositoryTests
{
    private readonly Mock<UserManager<User>> _userMgrMock;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        _userMgrMock = BuildUserManagerMock();
        _sut = new UserRepository(_userMgrMock.Object, /* dbContext */);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsUsersOrderedByCreatedAtDescending()
    {
        // Arrange
        var users = new List<User>
        {
            new() { CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { CreatedAt = DateTime.UtcNow },
        }.AsQueryable();
        _userMgrMock.Setup(m => m.Users).Returns(users);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].CreatedAt > result[1].CreatedAt);
    }
}
```

### Moq Rules
- `Setup(...).ReturnsAsync(...)` for async methods.
- `Verify(...)` to assert interactions — `Times.Once`, `Times.Never`, `Times.Exactly(n)`.
- Mock only dependencies — never mock the SUT itself.

### Reusable Mock Builders
```csharp
private static Mock<UserManager<User>> BuildUserManagerMock()
{
    var store = new Mock<IUserStore<User>>();
    return new Mock<UserManager<User>>(
        store.Object, null, null, null, null, null, null, null, null);
}

private static Mock<SignInManager<User>> BuildSignInManagerMock(Mock<UserManager<User>> userMgr)
{
    var contextAccessor = new Mock<IHttpContextAccessor>();
    var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
    return new Mock<SignInManager<User>>(
        userMgr.Object, contextAccessor.Object, claimsFactory.Object, null, null, null, null);
}
```

---

## Integration Tests (WebApplicationFactory)

Use for: HTTP endpoints, full request pipeline, auth flows, redirect behavior.

### Factory Pattern

```csharp
public class FeatureWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbPath = $"test-{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "",        // suppress seeder
                ["Seed:AdminPassword"] = "",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite($"Data Source={_dbPath}"));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    public HttpClient CreateClientWithoutAutoRedirect() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task SeedUserAsync(string email, string password, bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User { UserName = email, Email = email, FullName = "Test User", IsActive = isActive };
        await userManager.CreateAsync(user, password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
```

### Anti-Forgery Token Helper
```csharp
private static async Task<string> FetchAntiforgeryTokenAsync(HttpClient client, string path)
{
    var html = await (await client.GetAsync(path)).Content.ReadAsStringAsync();
    const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
    var start = html.IndexOf(marker) + marker.Length;
    return html[start..html.IndexOf('"', start)];
}
```

### What to Cover in Integration Tests
- Happy path: correct HTTP status + redirect location
- Auth failure: invalid credentials → stays on form
- Deactivation guard: deactivated user → signed out + error
- Lockout: too many failures → lockout message
- Security boundary: unauthenticated → redirect to `/auth/login`
- Cookie: `RememberMe=true` → `max-age` header present

---

## When to Write Unit vs Integration Tests

| Scenario | Test Type |
|---|---|
| PageModel handler logic (OnPostAsync) | Unit (Moq) |
| Repository method logic | Unit (Moq) |
| Full HTTP request pipeline | Integration (WebApplicationFactory) |
| Auth flow (login, redirect, cookie) | Integration |
| Seeder logic | Unit (Moq) |
| Blazor component with complex logic | Unit or Integration |

---

## Arrange / Act / Assert

Every test has exactly three sections, separated by blank lines:

```csharp
[Fact]
public async Task CreateAsync_ValidInput_ReturnsTrueAndCreatesUser()
{
    // Arrange
    _userMgrMock.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
        .ReturnsAsync(IdentityResult.Success);

    // Act
    var (success, errors) = await _sut.CreateAsync("user@test.com", "Test@123", "Admin", "Test User");

    // Assert
    Assert.True(success);
    Assert.Empty(errors);
}
```

- One logical behavior per test.
- Prefer specific assertions: `Assert.Equal("Done", status)` not just `Assert.NotNull(result)`.

---

## RED Phase Checklist
- [ ] Test compiles
- [ ] `dotnet test --filter` runs the test
- [ ] Test **fails** with a meaningful message (not a compile or setup error)

## GREEN Phase Checklist
- [ ] Wrote minimum code to pass — no extra logic
- [ ] Target test passes
- [ ] No other tests broken

## REFACTOR Phase Checklist
- [ ] Naming matches project conventions (see `code-review-lotplapp` skill)
- [ ] No duplication introduced
- [ ] Full `dotnet test` suite passes
