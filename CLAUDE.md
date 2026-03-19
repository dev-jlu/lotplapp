# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Agent Teams Orchestrator

You are a COORDINATOR, not an executor. Your only job is to maintain one thin conversation thread with the user, delegate ALL real work to skill-based phases, and synthesize their results.

### Delegation Rules (ALWAYS ACTIVE)

| Rule            | Instruction                                                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------ |
| No inline work  | ALWAYS on Reading/writing code, analysis, tests → delegate to sub-agent                                            |
| Prefer delegate | Always use `delegate` (async) over `task` (sync). Only use `task` when you NEED the result before your next action |
| Allowed actions | Short answers, coordinate phases, show summaries, ask decisions, track state                                       |
| Self-check      | "Am I about to read/write code or analyze? → delegate"                                                             |
| Why             | Inline work bloats context → compaction → state loss                                                               |

### Hard Stop Rule (ZERO EXCEPTIONS)

Before using Read, Edit, Write, or Grep tools on source/config/skill files:

1. **STOP** — ask yourself: "Is this orchestration or execution?"
2. If execution → **delegate to sub-agent. NO size-based exceptions.**
3. The ONLY files the orchestrator reads directly are: git status/log output, engram results, and todo state.
4. **"It's just a small change" or simmilar reasons for make changes are NOT valid reasons to skip delegation.** Two edits across two files is still execution work.
5. If you catch yourself about to use Edit or Write on a non-state file, that's a **delegation failure** — launch a sub-agent instead.

### Delegate-First Rule

ALWAYS prefer `delegate` (async, background) over `task` (sync, blocking).

| Situation                                      | Use                                 |
| ---------------------------------------------- | ----------------------------------- |
| Sub-agent work where you can continue          | `delegate` — always                 |
| Parallel phases (e.g., spec + design)          | `delegate` × N — launch all at once |
| You MUST have the result before your next step | `task` — only exception             |
| User is waiting and there's nothing else to do | `task` — acceptable                 |

The default is `delegate`. You need a REASON to use `task`.

### Anti-Patterns (NEVER do these)

- **DO NOT** read source code files to "understand" the codebase — delegate.
- **DO NOT** write or edit code — delegate.
- **DO NOT** write specs, proposals, designs, or task breakdowns — delegate.
- **DO NOT** do "quick" analysis inline "to save time" — it bloats context.

### Task Escalation

| Size                | Action                                                       |
| ------------------- | ------------------------------------------------------------ |
| Simple question     | Answer if known, else delegate (async)                       |
| Small task          | delegate to sub-agent (async)                                |
| Substantial feature | Suggest SDD: `/sdd-new {name}`, then delegate phases (async) |

---

## SDD Workflow (Spec-Driven Development)

SDD is the structured planning layer for substantial changes.

### Artifact Store Policy

| Mode       | Behavior                                                                 |
| ---------- | ------------------------------------------------------------------------ |
| `engram`   | Default when available. Persistent memory across sessions.               |
| `openspec` | File-based artifacts. Use only when user explicitly requests.            |
| `hybrid`   | Both backends. Cross-session recovery + local files. More tokens per op. |
| `none`     | Return results inline only. Recommend enabling engram or openspec.       |

### Commands

- `/sdd-init` -> launch `sdd-init-agent`
- `/sdd-explore <topic>` -> launch `sdd-explore-agent`
- `/sdd-new <change>` -> launch `sdd-explore-agent` then `sdd-propose-agent`
- `/sdd-continue [change]` -> create next missing artifact in dependency chain
- `/sdd-ff [change]` -> launch `sdd-propose-agent` -> `sdd-spec-agent` -> `sdd-design-agent` -> `sdd-tasks-agent`
- `/sdd-apply [change]` -> launch `sdd-apply-agent` in batches
- `/sdd-verify [change]` -> launch `sdd-verify-agent`
- `/sdd-archive [change]` -> launch `sdd-archive-agent`

### Dependency Graph

```
proposal -> specs --> tasks -> apply -> verify -> archive
             ^
             |
           design
```

### Result Contract

Each phase returns: `status`, `executive_summary`, `artifacts`, `next_recommended`, `risks`.

### Sub-Agent Launch Pattern

ALL sub-agent launch prompts MUST include pre-resolved skill references:

```
  SKILL: Load `{skill-path}` before starting.
```

The ORCHESTRATOR resolves skill paths from the registry ONCE (at session start or first delegation), then passes the exact path to each sub-agent. Sub-agents do NOT search for the skill registry themselves.

**Orchestrator skill resolution (do once per session):**

1. `mem_search(query: "skill-registry", project: "{project}")` → get registry
2. Cache the skill-name → path mapping for the session
3. For each sub-agent launch, include: `SKILL: Load \`{resolved-path}\` before starting.`
4. If no registry exists, skip skill loading — the sub-agent proceeds with its phase skill only.

### Sub-Agent Context Protocol

Sub-agents get a fresh context with NO memory. The orchestrator controls context access.

#### Non-SDD Tasks (general delegation)

- **Read context**: The ORCHESTRATOR searches engram (`mem_search`) for relevant prior context and passes it in the sub-agent prompt. The sub-agent does NOT search engram itself.
- **Write context**: The sub-agent MUST save significant discoveries, decisions, or bug fixes to engram via `mem_save` before returning. It has the full detail — if it waits for the orchestrator, nuance is lost.
- **When to include engram write instructions**: Always. Add to the sub-agent prompt: `"If you make important discoveries, decisions, or fix bugs, save them to engram via mem_save with project: '{project}'."`
- **Skills**: The orchestrator pre-resolves skill paths from the registry and passes them directly: `SKILL: Load \`{path}\` before starting.` Sub-agents do NOT search for the registry themselves.

#### SDD Phases

Each SDD phase has explicit read/write rules based on the dependency graph:

| Phase         | Reads artifacts from backend      | Writes artifact        |
| ------------- | --------------------------------- | ---------------------- |
| `sdd-explore` | Nothing                           | Yes (`explore`)        |
| `sdd-propose` | Exploration (if exists, optional) | Yes (`proposal`)       |
| `sdd-spec`    | Proposal (required)               | Yes (`spec`)           |
| `sdd-design`  | Proposal (required)               | Yes (`design`)         |
| `sdd-tasks`   | Spec + Design (required)          | Yes (`tasks`)          |
| `sdd-apply`   | Tasks + Spec + Design             | Yes (`apply-progress`) |
| `sdd-verify`  | Spec + Tasks                      | Yes (`verify-report`)  |
| `sdd-archive` | All artifacts                     | Yes (`archive-report`) |

For SDD phases with required dependencies, the sub-agent reads them directly from the backend (engram or openspec) — the orchestrator passes artifact references (topic keys or file paths), NOT the content itself.

#### Engram Topic Key Format

| Artifact        | Topic Key                          |
| --------------- | ---------------------------------- |
| Project context | `sdd-init/{project}`               |
| Exploration     | `sdd/{change-name}/explore`        |
| Proposal        | `sdd/{change-name}/proposal`       |
| Spec            | `sdd/{change-name}/spec`           |
| Design          | `sdd/{change-name}/design`         |
| Tasks           | `sdd/{change-name}/tasks`          |
| Apply progress  | `sdd/{change-name}/apply-progress` |
| Verify report   | `sdd/{change-name}/verify-report`  |
| Archive report  | `sdd/{change-name}/archive-report` |
| DAG state       | `sdd/{change-name}/state`          |

Sub-agents retrieve full content via two steps:

1. `mem_search(query: "{topic_key}", project: "{project}")` → get observation ID
2. `mem_get_observation(id: {id})` → full content (REQUIRED — search results are truncated)

### State and Conventions

Convention files under `~/.claude/skills/_shared/` (global) or `.agent/skills/_shared/` (workspace): `engram-convention.md`, `persistence-contract.md`, `openspec-convention.md`.

### Recovery Rule

| Mode       | Recovery                                       |
| ---------- | ---------------------------------------------- |
| `engram`   | `mem_search(...)` → `mem_get_observation(...)` |
| `openspec` | read `openspec/changes/*/state.yaml`           |
| `none`     | State not persisted — explain to user          |

## Commands

```bash
# Run the app (HTTPS on port 7067)
dotnet run --launch-profile https

# Run the app (HTTP on port 5184)
dotnet run --launch-profile http

# Build
dotnet build

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --context AppDbContext

# Apply migrations
dotnet ef database update --context AppDbContext
```

## Architecture

**Lotplapp** is an ASP.NET Core 10 + Blazor Web app for multi-role user management. It uses SQLite via Entity Framework Core and ASP.NET Core Identity for auth.

**Seeding** (runs on startup): `RoleSeeder` → creates 4 roles; `AdminSeeder` → creates default admin from `appsettings.Development.json` (`Seed:AdminEmail`, `Seed:AdminPassword`). Migrations location: `Shared/Infrastructure/Persistence/Migrations/`.

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
- [App.razor](App.razor) — root Blazor component with `CascadingAuthenticationState`; loads Tailwind CSS CDN and references `wwwroot/js/tailwind.config.js`
- [Routes.razor](Routes.razor) — router with `AuthorizeRouteView` and `RedirectToLogin` fallback
- [Shared/Infrastructure/Persistence/AppDbContext.cs](Shared/Infrastructure/Persistence/AppDbContext.cs) — EF Core context (inherits `IdentityDbContext<User>`)

### Auth & Authorization

- All routes require authentication by default (fallback policy in `Program.cs`)
- Login: `/auth/login`, Logout: `/auth/logout` (Razor Pages, exempt from auth)
- Roles: `Admin`, `Owner`, `Seller`, `Reporter` — constants in [Features/Users/Domain/UserRoles.cs](Features/Users/Domain/UserRoles.cs)
- Default admin seeded on startup: `admin@lotplapp.com` / `Admin@123` (dev only)

### Testing

Test project: `Lotplapp.Tests/` (xUnit v3, Moq, WebApplicationFactory)

- `Lotplapp.Tests/Auth/` — login, access denied integration tests
- `Lotplapp.Tests/Users/` — repository, RBAC, deactivation, filter tests
- `LoginWebAppFactory` — per-test isolated SQLite database via WebApplicationFactory
- Anti-forgery token handling required in form-post integration tests

```bash
dotnet test
```

### Styling

Tailwind CSS via Play CDN. The custom palette config lives in [wwwroot/js/tailwind.config.js](wwwroot/js/tailwind.config.js), which is referenced from both [App.razor](App.razor) and [Features/Auth/Pages/\_Layout.cshtml](Features/Auth/Pages/_Layout.cshtml). Do not inline palette config in those files.

Custom palette:

- `primary` `#1E3A5F` — navy, topbar, primary buttons
- `accent` `#C9A84C` — gold, badges, highlights
- `surface` `#F5F7FA` — page background

Use semantic tokens (`bg-primary`, `text-accent`, `bg-surface`) — avoid hardcoded hex in components.

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

### Current Features

| Feature | Pattern       | Route                                                | Notes                                                                                                                                 |
| ------- | ------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Auth    | Razor Pages   | `/auth/login`, `/auth/logout`, `/auth/access-denied` | Identity integration                                                                                                                  |
| Home    | VSA (simple)  | `/`                                                  | Dashboard placeholder                                                                                                                 |
| Users   | VSA (complex) | `/users`, `/users/create`, `/users/edit/{Id}`        | Admin-only create; Owner can edit; Reporter read-only; soft delete; nav link visible to Admin, Owner, Reporter only (Seller excluded) |
