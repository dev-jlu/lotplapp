# Copilot Instructions for Lotplapp

**Context**: ASP.NET Core 10 + Blazor Web app for multi-role user management. Hybrid architecture: Modular Monolith + Vertical Slice Architecture (VSA) for simple features, Clean Architecture per slice for complex features.

## Quick Reference

### Build & Run

```bash
# Run app
dotnet run --launch-profile https      # HTTPS on port 7067
dotnet run --launch-profile http       # HTTP on port 5184

# Build & Test
dotnet build
dotnet test                            # Run all tests
dotnet test --filter <filter>          # Run specific test

# Database
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Technology Stack

- **Framework**: .NET 10, ASP.NET Core, Blazor Web (Server-side)
- **Database**: SQLite with EF Core 10.0.3
- **Auth**: ASP.NET Core Identity with role-based access (Admin, Owner, Seller, Reporter)
- **Testing**: xUnit v3, WebApplicationFactory, Moq 4.20.72
- **ORMapper**: Entity Framework Core 10.0.3

## Architecture & How To Implement Features

### Decision: VSA vs. Clean Architecture

**Start with VSA (Vertical Slice)** for:

- Simple features with minimal business logic
- CRUD operations without complex workflows
- Features without significant cross-cutting concerns

**Upgrade to Clean Architecture** if:

- Feature has non-trivial business logic, use cases, or orchestration
- Needs to be independently tested beyond integration tests
- Has multiple entry points (API, Blazor, background jobs)

### Vertical Slice Architecture (VSA)

```
Features/<FeatureName>/
├── Domain/
│   ├── <Entity>.cs              # Data model
│   ├── I<Name>Repository.cs     # Repository interface
│   └── <Constants>.cs           # Role/status constants
├── Infrastructure/
│   ├── <Name>Repository.cs      # EF Core implementation
│   └── ServiceRegistration.cs   # DI helpers (optional)
└── Presentation/
    ├── <Feature>.razor          # Blazor component
    └── <Feature>.razor.cs       # Code-behind (partial class)
```

**Register in Program.cs**:

```csharp
builder.Services.AddScoped<I<Name>Repository, <Name>Repository>();
```

**Add to AppDbContext** (Shared/Infrastructure/Persistence/AppDbContext.cs):

```csharp
public DbSet<<Entity>> <Entities> { get; set; }
```

### Clean Architecture (Complex Features)

```
Features/<FeatureName>/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   └── Interfaces/
├── Application/
│   ├── UseCases/
│   ├── DTOs/
│   ├── IServices.cs
│   └── Validators/
├── Infrastructure/
│   ├── Repositories/
│   ├── Services/
│   └── External APIs/
└── Presentation/
    ├── <Feature>.razor
    └── <Feature>.razor.cs
```

**Register in Program.cs**:

```csharp
builder.Services.AddScoped<IRepository, Repository>();
builder.Services.AddScoped<IService, Service>();
```

## Code Patterns & Conventions

### Repository Pattern

- **Interface**: `Domain/I<Name>Repository.cs`
- **Implementation**: `Infrastructure/<Name>Repository.cs`
- **Error Handling**: Return `(bool Success, IEnumerable<string> Errors)` — never throw domain exceptions
- **Example**: [UserRepository.cs](../Features/Users/Infrastructure/UserRepository.cs)

### Blazor Components

- **Split files**: `<Feature>.razor` (markup) + `<Feature>.razor.cs` (code-behind)
- **Code-behind syntax**: `public partial class <Feature>`
- **Dependency Injection**: `[Inject] private I<Service> Service { get; set; } = default!;`
- **Initialization**: Use `OnInitializedAsync()` for async data loading
- **Render Mode**: Interactive: `@rendermode InteractiveServer`; Static: omit attribute
- **Avoid**: Never put business logic in `.razor.cs` — delegate to repositories/services

### Authentication & Authorization

- **Policy**: All routes require authentication by default (fallback policy in [Program.cs](../Program.cs))
- **Login/Logout**: Razor Pages in `Features/Auth/Pages/` (never Blazor)
- **Login URL**: `/auth/login`
- **Logout URL**: `/auth/logout`
- **Roles**: `Admin`, `Owner`, `Seller` — constants in [Features/Users/Domain/UserRoles.cs](../Features/Users/Domain/UserRoles.cs)
- **Default Admin** (dev only): `admin@lotplapp.com` / `Admin@123`
- **Authorize Blazor Pages**: Apply `@attribute [Authorize(Roles = "Admin")]` at top of `.razor`

### Entity Framework & Database

- **DbContext**: [Shared/Infrastructure/Persistence/AppDbContext.cs](../Shared/Infrastructure/Persistence/AppDbContext.cs)
- **Service Registration**: In [Program.cs](../Program.cs)
- **Migrations**: Use `dotnet ef migrations add <Name>` and commit to source control
- **Seeding**: Use IDataSeed implementations registered in Program.cs (not in migrations)

### C# Coding Standards

- **Modern syntax**: Collection expressions (`[]`, `[.. other]`), pattern matching (`is not null`), records for DTOs
- **Nullable**: Project has nullable enabled; use `!` only with comments
- **Async**: Always use async/await; methods ending in `Async` return `Task` or `Task<T>`
- **Naming**: `_camelCase` for private fields, `PascalCase` for properties, interfaces start with `I`
- **Comments**: Use XML documentation (`///`) for public members; explain _why_, not _what_

## Testing Conventions

### Integration Tests (Recommended)

- **Framework**: xUnit v3 with `[Fact]` and `[Theory]` attributes
- **Test Server**: `WebApplicationFactory<Program>` with IClassFixture
- **Database Isolation**: Each test class gets its own in-memory SQLite database
- **Setup Pattern**: See [LoginWebAppFactory.cs](../Lotplapp.Tests/Auth/LoginWebAppFactory.cs)
- **Anti-forgery**: Fetch token before POST in web-based tests
- **Seeding**: Use factory helper methods (e.g., `SeedUserAsync()`)

**Example Test Structure**:

```csharp
public class FeatureIntegrationTests : IClassFixture<TestWebAppFactory>
{
    [Fact]
    public async Task MethodName_Condition_ExpectedBehavior()
    {
        // Arrange: Set up test data via factory helper
        var client = _factory.CreateClient();

        // Act: Execute the feature
        var result = await client.GetAsync("/feature/endpoint");

        // Assert: Verify behavior
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }
}
```

### Unit Tests

- **Pure domain logic**: Test entities, value objects, constants without EF
- **Framework**: xUnit with `[Fact]` or `[Theory]` + `[InlineData(...)]`
- **Example**: [UserRolesTests.cs](../Lotplapp.Tests/Users/UserRolesTests.cs)

### Run Tests

```bash
dotnet test                                    # All tests
dotnet test Lotplapp.Tests --logger=console   # Verbose output
```

## Common Pitfalls & Solutions

### ❌ Pitfall 1: Business Logic in `.razor.cs`

**Problem**: Business logic in Blazor code-behind violates separation of concerns.
**Solution**: Keep `.razor.cs` minimal; delegate to repositories/services in Application/Infrastructure layers.

### ❌ Pitfall 2: Test Data Disappears

**Problem**: WebApplicationFactory doesn't run startup seeders; tests see empty database.
**Solution**: Use factory helper methods to seed test data (see [LoginWebAppFactory.cs](../Lotplapp.Tests/Auth/LoginWebAppFactory.cs)).

### ❌ Pitfall 3: Forgetting Anti-forgery Tokens

**Problem**: POST to Razor Pages fails with 400 Bad Request.
**Solution**: Fetch token from login page before POSTing; include in form data.

### ❌ Pitfall 4: Using `.Result` or `.Wait()`

**Problem**: Deadlocks, thread-pool starvation.
**Solution**: Always use `async/await` in integration tests and async handlers.

### ❌ Pitfall 5: Throwing Domain Exceptions from Repositories

**Problem**: Callers can't distinguish between expected business errors and true exceptions.
**Solution**: Return `(bool Success, IEnumerable<string> Errors)` from repository methods.

## File Structure Overview

```
Lotplapp/
├── Program.cs                          # DI, Identity config, seeding
├── App.razor                           # Root Blazor component
├── Routes.razor                        # Router setup
├── CLAUDE.md                           # Architecture docs
├── Features/                           # Feature modules
│   ├── Auth/Pages/                    # Login/Logout Razor Pages
│   ├── Users/                         # User management (VSA example)
│   └── <FeatureName>/                 # Other features
├── Shared/
│   ├── Infrastructure/Persistence/    # AppDbContext, migrations
│   ├── Layout/                        # MainLayout, shared components
│   ├── Auth/                          # Auth components, redirects
│   └── ErrorPages/                    # 404, 500 error pages
├── Lotplapp.Tests/
│   ├── Auth/
│   │   ├── LoginIntegrationTests.cs
│   │   ├── LoginWebAppFactory.cs      # Test isolation pattern
│   │   └── LoginModelTests.cs
│   └── Users/
│       └── UserRolesTests.cs          # Unit test example
└── .github/
    └── workflows/
        ├── claude-code-review.yml     # Auto-review PRs
        └── claude.yml                 # On-demand assistance
```

## Quick Decisions

| Scenario                                    | Action                                                                                                       |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Add simple CRUD feature                     | Use VSA: Domain + Infrastructure + Presentation                                                              |
| Feature requires business logic / use cases | Use Clean Architecture with Application layer                                                                |
| Add tests for domain logic                  | xUnit `[Fact]` in Lotplapp.Tests/<Feature>/                                                                  |
| Add integration tests                       | xUnit with WebApplicationFactory + isolated DB                                                               |
| Need to seed test data                      | Use factory helper method (see LoginWebAppFactory pattern)                                                   |
| Add new user role                           | Add constant to [UserRoles.cs](../Features/Users/Domain/UserRoles.cs) and update auth policies in Program.cs |
| Add database model                          | Create entity in Features/<Name>/Domain/, add to AppDbContext, run `dotnet ef migrations add`                |
| Deploy changes                              | Workflows defined in .github/workflows/ — auto-triggered on PR, manual via @claude mention                   |

---

# Agent Teams Lite — Lean Orchestrator for VS Code Copilot

Add this to `.github/copilot-instructions.md` in your project root.

## Spec-Driven Development (SDD)

You are the SDD orchestrator. Keep the same assistant identity and apply SDD as an overlay.

### Core Operating Rules

- Delegate-only: never do analysis/design/implementation/verification inline.
- Use Task/sub-agent execution if available; otherwise run the phase skill inline.
- The lead only coordinates DAG state, user approvals, and concise summaries.
- `/sdd-new`, `/sdd-continue`, and `/sdd-ff` are meta-commands handled by the orchestrator (not skills).

### Artifact Store Policy

- `artifact_store.mode`: `engram | openspec | hybrid | none`
- Default: `engram` when available; `openspec` only if user explicitly requests file artifacts; `hybrid` for both backends simultaneously; otherwise `none`.
- `hybrid` persists to BOTH Engram and OpenSpec. Provides cross-session recovery + local file artifacts. Consumes more tokens per operation.
- In `none`, do not write project files. Return results inline and recommend enabling `engram` or `openspec`.

### Commands

- `/sdd-init` -> run `sdd-init`
- `/sdd-explore <topic>` -> run `sdd-explore`
- `/sdd-new <change>` -> run `sdd-explore` then `sdd-propose`
- `/sdd-continue [change]` -> create next missing artifact in dependency chain
- `/sdd-ff [change]` -> run `sdd-propose` -> `sdd-spec` -> `sdd-design` -> `sdd-tasks`
- `/sdd-apply [change]` -> run `sdd-apply` in batches
- `/sdd-verify [change]` -> run `sdd-verify`
- `/sdd-archive [change]` -> run `sdd-archive`

### Dependency Graph

```
proposal -> specs --> tasks -> apply -> verify -> archive
             ^
             |
           design
```

### Result Contract

Each phase returns: `status`, `executive_summary`, `artifacts`, `next_recommended`, `risks`.

### State and Conventions (source of truth)

Keep this file lean. Do not inline full persistence or naming specs here.

Use shared convention files under `.vscode/skills/_shared/` (or your configured skills path):

- `engram-convention.md` for artifact naming and two-step recovery
- `persistence-contract.md` for mode behavior and state persistence/recovery
- `openspec-convention.md` for file layout when mode is `openspec`

### Recovery Rule

If SDD state is missing (for example after context compaction), recover before continuing:

- `engram`: `mem_search(...)` then `mem_get_observation(...)`
- `openspec`: read `openspec/changes/*/state.yaml`
- `none`: explain that state was not persisted

### SDD Suggestion Rule

For substantial features/refactors, suggest SDD.
For small fixes/questions, do not force SDD.
