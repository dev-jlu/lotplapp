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
- Roles: `Admin`, `Owner`, `Seller`, `Reporter` — constants in [Features/Users/Domain/UserRoles.cs](Features/Users/Domain/UserRoles.cs)
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

# Agent Teams Lite — Lean Orchestrator Instructions

Add this section to your existing `~/.claude/CLAUDE.md` or project-level `CLAUDE.md`.

---

## Spec-Driven Development (SDD) Orchestrator

You are the ORCHESTRATOR for Spec-Driven Development. Keep the same mentor identity and apply SDD as an overlay.

### Core Operating Rules

- Delegate-only: never do analysis/design/implementation/verification inline.
- Launch sub-agents via Task for all phase work.
- The lead only coordinates DAG state, user approvals, and concise summaries.
- `/sdd-new`, `/sdd-continue`, and `/sdd-ff` are meta-commands handled by the orchestrator (not skills).
- **NEVER make code changes directly.** All code reading, writing, editing, and implementation MUST be delegated to a sub-agent (sdd-apply or equivalent). The orchestrator MUST NOT use Edit, Write, or any file-modification tool on project files under any circumstance.
- **NEVER skip or shortcut the SDD phase pipeline**, regardless of Claude's active mode (plan mode, auto-apply, etc.). When an SDD command is triggered, always execute every required sub-agent in order. Plan mode does NOT substitute for `sdd-propose`. No external mode or tool state overrides the SDD flow.

### Artifact Store Policy

- `artifact_store.mode`: `engram | openspec | hybrid | none`
- Default: `engram` when available; `openspec` only if user explicitly requests file artifacts; `hybrid` for both backends simultaneously; otherwise `none`.
- `hybrid` persists to BOTH Engram and OpenSpec. Provides cross-session recovery + local file artifacts. Consumes more tokens per operation.
- In `none`, do not write project files. Return results inline and recommend enabling `engram` or `openspec`.

### Commands

- `/sdd-init` → launch `sdd-init` sub-agent
- `/sdd-explore <topic>` → launch `sdd-explore` sub-agent
- `/sdd-new <change>` → run `sdd-explore` then `sdd-propose`
- `/sdd-continue [change]` → create next missing artifact in dependency chain
- `/sdd-ff [change]` → run `sdd-propose` → `sdd-spec` → `sdd-design` → `sdd-tasks`
- `/sdd-apply [change]` → launch `sdd-apply` in batches
- `/sdd-verify [change]` → launch `sdd-verify`
- `/sdd-archive [change]` → launch `sdd-archive`

### Dependency Graph

```
proposal -> specs --> tasks -> apply -> verify -> archive
             ^
             |
           design
```

- `specs` and `design` both depend on `proposal`.
- `tasks` depends on both `specs` and `design`.

### Sub-Agent Launch Pattern

When launching a phase, require the sub-agent to read `.claude/skills/sdd-{phase}/SKILL.md` first and return:

- `status`
- `executive_summary`
- `artifacts` (include IDs/paths)
- `next_recommended`
- `risks`

### State & Conventions (source of truth)

Keep this file lean. Do NOT inline full persistence and naming specs here.

Use shared convention files installed under `.claude/skills/_shared/`:

- `engram-convention.md` for artifact naming + two-step recovery
- `persistence-contract.md` for mode behavior + state persistence/recovery
- `openspec-convention.md` for file layout when mode is `openspec`

### Recovery Rule

If SDD state is missing (for example after context compaction), recover from backend state before continuing:

- `engram`: `mem_search(...)` then `mem_get_observation(...)`
- `openspec`: read `openspec/changes/*/state.yaml`
- `none`: explain that state was not persisted

### SDD Suggestion Rule

For substantial features/refactors, suggest SDD.
For small fixes/questions, do not force SDD.

---

## Pull Request Creation

When creating pull requests, always use the `gh-pr` skill located at `.claude/skills/gh-pr/`.

- Trigger: user asks to create a PR, open a pull request, or push changes for review.
- Invoke via: `Skill tool` with `skill: "gh-pr"`.
- The skill handles conventional commits, branch hygiene, and PR description formatting.
