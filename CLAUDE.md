# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the app (HTTPS on port 7067)
dotnet run --launch-profile https

# Run the app (HTTP on port 5184)
dotnet run --launch-profile http

# Build
dotnet build

# Add a new EF Core migration
dotnet ef migrations add <MigrationName>

# Apply migrations
dotnet ef database update
```

## Architecture

**Lotplapp** is an ASP.NET Core 10 + Blazor Web app for multi-role user management. It uses SQLite via Entity Framework Core and ASP.NET Core Identity for auth.

### Structure Pattern

The project uses a **hybrid architecture**: Modular Monolith + Vertical Slice Architecture (VSA) for simple features, and Clean Architecture per slice for complex features.

**Simple features** (no significant business logic) → VSA layout:
- `Features/<FeatureName>/Domain/` — entities, interfaces, constants
- `Features/<FeatureName>/Infrastructure/` — repository implementations
- `Features/<FeatureName>/Presentation/` — Blazor `.razor` components (pages)

**Complex features** (non-trivial business logic, use cases, orchestration) → Clean Architecture per feature:
- `Features/<FeatureName>/Domain/` — entities, value objects, domain interfaces
- `Features/<FeatureName>/Application/` — use cases / commands / queries, DTOs, service interfaces
- `Features/<FeatureName>/Infrastructure/` — repository implementations, external integrations
- `Features/<FeatureName>/Presentation/` — Blazor `.razor` components (pages)

**Shared concerns:**
- `Features/Auth/Pages/` — Razor Pages for login/logout (not Blazor)
- `Shared/` — cross-cutting: layouts, error pages, auth helpers, persistence (AppDbContext)

### Key Entry Points

- [Program.cs](Program.cs) — DI registration, Identity config, auth pipeline, database seeding
- [App.razor](App.razor) — root Blazor component with `CascadingAuthenticationState`
- [Routes.razor](Routes.razor) — router with `AuthorizeRouteView` and `RedirectToLogin` fallback
- [Shared/Infrastructure/Persistence/AppDbContext.cs](Shared/Infrastructure/Persistence/AppDbContext.cs) — EF Core context (inherits `IdentityDbContext<User>`)

### Auth & Authorization

- All routes require authentication by default (fallback policy in `Program.cs`)
- Login: `/auth/login`, Logout: `/auth/logout` (Razor Pages, exempt from auth)
- Roles: `Admin`, `Owner`, `Seller` — constants in [Features/Users/Domain/UserRoles.cs](Features/Users/Domain/UserRoles.cs)
- Default admin seeded on startup: `admin@lotplapp.com` / `Admin@123` (dev only)

### Adding a New Feature

**Simple feature (VSA):**
1. `Features/<Name>/Domain/` — entity + `I<Name>Repository` interface
2. `Features/<Name>/Infrastructure/<Name>Repository.cs` — implementing the interface
3. `Features/<Name>/Presentation/<Name>.razor` — Blazor component
4. Register repository in `Program.cs`
5. Add `DbSet` to `AppDbContext` and run `dotnet ef migrations add`

**Complex feature (Clean Architecture per slice):**
1. `Features/<Name>/Domain/` — entity, value objects, domain interfaces
2. `Features/<Name>/Application/` — use case classes / CQRS handlers, DTOs, `I<Service>` interfaces
3. `Features/<Name>/Infrastructure/` — repository and service implementations
4. `Features/<Name>/Presentation/<Name>.razor` — Blazor component calling application services
5. Register all services/repos in `Program.cs`
6. Add `DbSet` to `AppDbContext` and run `dotnet ef migrations add`

**Decision rule:** start with VSA; upgrade to Clean Architecture if the feature has meaningful business logic, multiple use cases, or needs to be tested in isolation.
