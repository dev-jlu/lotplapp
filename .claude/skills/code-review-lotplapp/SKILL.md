---
name: code-review-lotplapp
description: >
  Code review skill for the Lotplapp ASP.NET Core 10 + Blazor Web project.
  Enforces C# / Blazor best practices, project-specific conventions (VSA + repository pattern),
  Identity auth patterns, and test quality standards.
  Trigger: When reviewing PRs, changed files, or code in the Lotplapp project.
license: Apache-2.0
metadata:
  author: dev-jlu
  version: "1.0"
---

## When to Use

- Reviewing a GitHub PR for the Lotplapp project
- Auditing a feature branch before merge
- Checking new feature code for convention compliance
- Validating test coverage for a new change

---

## Review Checklist

Work through these sections in order. Flag every violation with:

> **[SEVERITY]** `file:line` — description + suggested fix

Severities: `BLOCKER` | `MAJOR` | `MINOR` | `NIT`

---

## 1 — Architecture & Structure

### 1.1 Feature Placement
- All code lives under `Features/<FeatureName>/` or `Shared/` — no orphaned files in root.
- Simple features (no business logic) → VSA layout:
  - `Domain/` — entities + `I<Name>Repository`
  - `Infrastructure/` — repository implementation
  - `Presentation/` — `.razor` + `.razor.cs`
- Complex features (orchestration, multiple use cases) → Clean Architecture per slice: add `Application/` layer.
- **Flag**: business logic placed directly in Presentation (razor.cs) rather than Application or Repository.

### 1.2 Shared Concerns
- Cross-cutting code goes in `Shared/`: layouts, auth helpers, `AppDbContext`, seeders.
- Auth pages (Login, Logout) are Razor Pages in `Features/Auth/Pages/` — never Blazor components.

### 1.3 New Feature Registration
Every new feature must:
- [ ] Register repository in `Program.cs`: `builder.Services.AddScoped<IRepo, Impl>()`
- [ ] Add `DbSet<T>` to `AppDbContext` (if new entity)
- [ ] Include EF migration (if schema changed)

---

## 2 — C# Language & Conventions

### 2.1 Naming
| Element | Pattern | Example |
|---|---|---|
| Classes, interfaces | PascalCase | `UserRepository`, `IUserRepository` |
| Private fields | `_camelCase` | `_userManager`, `_isLoading` |
| Methods | PascalCase | `GetAllAsync`, `HandleSubmit` |
| Async methods | Suffix `Async` | `CreateAsync`, `SeedAsync` |
| Local variables | camelCase | `email`, `adminUser` |
| Constants / role strings | static const fields | `UserRoles.Admin` |
| Blazor components | PascalCase nouns | `UserList`, `CreateUser` |

### 2.2 Nullable Reference Types
- Project uses `<Nullable>enable</Nullable>` — **no suppression (`!`) without comment**.
- Uninitialized injected dependencies: use `= default!` with `[Inject]` (Blazor) or constructor injection.
- Optional state variables: use `?` (e.g., `List<User>?`) and guard with `@if (_users is null)`.

### 2.3 C# Modern Syntax (required)
- Collection expressions: `[]` for empty, `[.. other]` for spread — **not** `new List<T>()`.
- Pattern matching: `if (x is not null)` — **not** `x != null` for reference checks.
- Target-typed new: `new()` when type is obvious from context.
- Tuples for result/error: `(bool Success, IEnumerable<string> Errors)` from repositories.
- `string.IsNullOrWhiteSpace(x)` — not `x == null || x == ""`.

### 2.4 Async/Await
- All data access must be `async Task<T>` — no `.Result` or `.Wait()`.
- `ConfigureAwait(false)` not required (ASP.NET Core sync context is OK).
- `OnInitializedAsync` is the correct Blazor async lifecycle hook.

### 2.5 Error Handling
- Repositories return tuples: `(bool, IEnumerable<string>)` — **not** raw exceptions.
- Never swallow exceptions silently — log via `ILogger<T>`.
- Use `ILogger.LogInformation` / `LogWarning` / `LogError` at appropriate levels.

---

## 3 — Repository Pattern

### 3.1 Interface in Domain
```csharp
// Features/<Name>/Domain/I<Name>Repository.cs
public interface IUserRepository
{
    Task<List<User>> GetAllAsync();
    Task<(bool Success, IEnumerable<string> Errors)> CreateAsync(...);
}
```

### 3.2 Implementation in Infrastructure
```csharp
// Features/<Name>/Infrastructure/<Name>Repository.cs
public class UserRepository : IUserRepository
{
    private readonly UserManager<User> _userManager;   // Identity
    private readonly AppDbContext _dbContext;           // EF (raw queries)

    public async Task<List<User>> GetAllAsync()
        => await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
}
```

### 3.3 Rules
- Repositories use `UserManager<User>` for Identity-related CRUD (create, find by email, roles).
- Use `AppDbContext` directly only for queries not covered by `UserManager`.
- Never put EF/Identity calls in Blazor components — always go through the repository interface.

---

## 4 — Blazor Components

### 4.1 Component + Code-Behind Split
- `.razor` file: markup, directives, `@page`, `@attribute`, `@inject` (or `@code { }` for tiny components).
- `.razor.cs` file: `public partial class` with `[Inject]` properties, private state fields, lifecycle methods.

### 4.2 Render Mode
- Interactive UI → `@rendermode InteractiveServer` at component or page level.
- Static server-rendered pages (no interactivity needed) → **no rendermode** directive.
- Auth pages (Login/Logout) → Razor Pages, **not** Blazor components.

### 4.3 DI in Components
```csharp
[Inject]
private IUserRepository UserRepository { get; set; } = default!;

[Inject]
private NavigationManager NavigationManager { get; set; } = default!;
```
- Use `[Inject]` attribute — not constructor injection — for Blazor components.
- Always initialize with `= default!` to satisfy nullable analysis.

### 4.4 State Pattern
```csharp
private List<User>? _users;       // null = loading; empty list = no data
private List<string> _errors = [];
private bool _isLoading;

protected override async Task OnInitializedAsync()
{
    _users = await UserRepository.GetAllAsync();
}
```

### 4.5 Form Handling
```csharp
private async Task HandleSubmit()
{
    _errors = [];
    if (string.IsNullOrWhiteSpace(_field)) _errors.Add("Field is required.");
    if (_errors.Count != 0) return;

    _isLoading = true;
    var (success, errors) = await Repository.CreateAsync(...);
    if (success) NavigationManager.NavigateTo("/route");
    else _errors = [.. errors];
    _isLoading = false;
}
```

### 4.6 Authorization
```csharp
@attribute [Authorize]                           // Any authenticated user
@attribute [Authorize(Roles = "Admin,Owner")]    // Role-restricted page
```
- Fallback policy in `Program.cs` already requires auth — no need to repeat `[Authorize]` unless role-restricting.

---

## 5 — ASP.NET Core Identity & Auth

### 5.1 Deactivation Guard (existing pattern — must be preserved)
```csharp
// After PasswordSignInAsync succeeds, ALWAYS check IsActive:
var user = await _userManager.FindByEmailAsync(Input.Email);
if (user is not null && !user.IsActive)
{
    await _signInManager.SignOutAsync();
    ModelState.AddModelError(string.Empty, "Your account has been deactivated.");
    return Page();
}
```
- **Flag**: any new auth flow that doesn't check `IsActive` after successful sign-in.

### 5.2 Session Expiry (existing pattern — must be preserved)
- `ExpireTimeSpan = TimeSpan.FromHours(8)` + `SlidingExpiration = true` in `ConfigureApplicationCookie`.
- Do not remove or widen these settings without justification.

### 5.3 Security Checklist
- [ ] No user enumeration: use the same generic error for invalid credentials, locked accounts, etc.
- [ ] AntiforgeryToken on all Razor Page POSTs.
- [ ] `[AllowAnonymous]` only on Login and Logout pages — never on data-access routes.
- [ ] `lockoutOnFailure: true` in `PasswordSignInAsync` calls.
- [ ] Password validation config (Program.cs) — do not weaken below current settings.

---

## 6 — Entity Framework & Database

### 6.1 Entities
- Custom entity properties added to `User` (extends `IdentityUser`) must have defaults:
  ```csharp
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  ```
- New entities follow the same pattern: defaults in property initializer.

### 6.2 Migrations
- **Every schema change** must include an EF migration: `dotnet ef migrations add <Name>`.
- Migration name: PascalCase, descriptive — `AddProductEntity`, `AddIsActiveToUser`.
- **Flag**: schema change (new `DbSet`, new property) without accompanying migration file.

### 6.3 Query Patterns
- Prefer `UserManager` methods over raw EF for Identity entities.
- For custom queries: use `_dbContext.Set<T>()` or typed `DbSet` — never raw SQL strings.
- Always use async EF methods: `.ToListAsync()`, `.FirstOrDefaultAsync()`, etc.

---

## 7 — Dependency Injection

### 7.1 Lifetime Rules
| Type | Lifetime |
|---|---|
| Repositories | `Scoped` |
| EF DbContext | `Scoped` |
| Seeders | `Scoped` (startup-only) |
| Stateless utilities | `Singleton` |

### 7.2 Registration in Program.cs
```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
```
- Always register via **interface**, not concrete type (except seeders).
- Order: DbContext → Identity → Seeders → Repositories → Razor Pages/Blazor.

---

## 8 — Seeders

### 8.1 Seeder Pattern
```csharp
public class SomeSeeder(ILogger<SomeSeeder> logger, ...)
{
    public async Task SeedAsync()
    {
        if (await AlreadyExists()) { logger.LogInformation("Skipping..."); return; }
        // create...
        logger.LogInformation("Seeded {Entity}.", name);
    }
}
```
- Seeders must be **idempotent** — check existence before creating.
- Log at `LogInformation` level for created, skipped.
- New seeders: register in `Program.cs` and call from `DatabaseSeeder.SeedAsync()`.

---

## 9 — Testing

### 9.1 Test Project Location
`Lotplapp.Tests/` — mirror feature path: `Auth/LoginModelTests.cs` → `Features/Auth/`.

### 9.2 Unit Tests (Moq)
```csharp
[Fact]
public async Task OnPostAsync_DeactivatedUser_SignsOutAndReturnsPageWithError()
{
    // Arrange
    signInMock.Setup(s => s.PasswordSignInAsync(...)).ReturnsAsync(SignInResult.Success);
    userMgrMock.Setup(u => u.FindByEmailAsync(...)).ReturnsAsync(new User { IsActive = false });

    // Act
    var result = await model.OnPostAsync();

    // Assert
    Assert.IsType<PageResult>(result);
    signInMock.Verify(s => s.SignOutAsync(), Times.Once);
}
```
- Method names: `MethodName_Scenario_ExpectedBehavior`.
- One logical assertion group per test (Arrange/Act/Assert).
- Use `Moq.Setup` + `Verify` — no interaction testing via mock call counts only.

### 9.3 Integration Tests (WebApplicationFactory)
```csharp
public class FeatureIntegrationTests : IClassFixture<FeatureWebAppFactory> { }
```
- Each test class gets its own isolated SQLite database (`Guid.NewGuid():N` path).
- Suppress seeder via config: inject empty `Seed:AdminEmail` / `Seed:AdminPassword`.
- Test happy path + all error paths + security boundaries (401, 403, redirect).
- Fetch anti-forgery token before POST requests.
- Assert HTTP status codes **and** response headers/location.

### 9.4 Coverage Expectations
- New auth/security flows: **100% unit + integration coverage**.
- New repository methods: **at least unit tests**.
- Blazor presentation components: integration or UI tests if logic is non-trivial.

---

## 10 — Security

- [ ] No hardcoded credentials, secrets, or connection strings in committed code.
- [ ] `appsettings.Development.json` secrets (Seed:AdminPassword) — acceptable only for dev seeds.
- [ ] No XSS: use `@variable` (Blazor auto-encodes) — never `@((MarkupString)variable)` on user data.
- [ ] SQL injection: always use EF parameterized queries — never string interpolation in queries.
- [ ] CSRF: `AntiforgeryToken` on all Razor Page forms; Blazor forms are CSRF-safe by default.
- [ ] Authorization: every new route/component must explicitly handle authorization (role or fallback policy).

---

## 11 — Code Quality

- [ ] No dead code (unused methods, fields, using directives).
- [ ] No TODO comments without a linked issue.
- [ ] Methods > 30 lines: consider extraction.
- [ ] No duplicate logic between similar features — extract to `Shared/` if reused in 2+ places.
- [ ] Logging: use structured logging `_logger.LogWarning("User {UserId} deactivated.", user.Id)` — not string concatenation.

---

## Output Format

Return the review as a Markdown report with these sections:

```markdown
## Code Review: <PR title or branch>

### Summary
<2-3 sentence overview of what the change does and overall quality>

### Blockers
<list of BLOCKER items, or "None">

### Major Issues
<list of MAJOR items, or "None">

### Minor / Nit
<list of MINOR and NIT items, or "None">

### Security Observations
<security-specific notes>

### Test Coverage
<assessment of test quality and coverage gaps>

### Verdict
APPROVE | REQUEST CHANGES | NEEDS DISCUSSION
```

---

## Commands

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Build to catch compile errors
dotnet build

# Check for EF migration consistency
dotnet ef migrations list

# Run app locally
dotnet run --launch-profile https
```

## Resources

- **Architecture guide**: See [CLAUDE.md](../../../CLAUDE.md)
- **Project structure**: `Features/` (features) + `Shared/` (cross-cutting) + `Lotplapp.Tests/` (tests)
- **Auth entry points**: `Features/Auth/Pages/Login.cshtml.cs`, `Program.cs` (cookie config)
- **Test examples**: `Lotplapp.Tests/Auth/LoginModelTests.cs`, `LoginIntegrationTests.cs`
