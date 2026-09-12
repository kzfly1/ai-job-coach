# Claude Instructions — AI Job Coach

Repository-specific rules and boundaries. Generic workflow — how to brainstorm, plan,
delegate, verify, and review — comes from Orca and Superpowers and is not duplicated here.

## Orientation

Establish current state by inspecting the repository, not by reading documentation. The
code is the only accurate record of what exists.

Two documents carry durable knowledge worth loading:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — system shape, module and layer
  conventions, integration seams. Read the relevant section when a task crosses a layer
  or module boundary, or introduces a new seam.
- [docs/DECISIONS.md](docs/DECISIONS.md) — why the constraints below exist, and what
  would justify revisiting them. Read when a task presents a trade-off, or when a rule
  here seems wrong for the task at hand.

Load a section, not a document. Workers should receive the minimum context their task
requires.

## Scope Control

- Build only what the current task requires. Do not add scope.
- Prefer small, focused changes and vertical slices over broad rewrites.
- Do not mix unrelated refactoring into feature work.
- Do not create a module folder, table, endpoint, or component before a task requires it.
  No placeholder folders, no stub endpoints, no speculative abstractions.
- If a task is taking longer than expected, cut scope within it — deliver the working
  core and surface what was deferred. Do not silently narrow or widen the deliverable.
- A new idea that arrives mid-task does not get built in that task.
- No infrastructure component is added without a concrete, current need.
- Preserve existing conventions. Match the surrounding code.

## Architectural Guardrails

These are the durable constraints of this codebase. Each exists for a documented reason —
see [docs/DECISIONS.md](docs/DECISIONS.md) before proposing an exception.

**Do not introduce:** the repository pattern · AutoMapper or any convention-based mapper ·
MediatR, CQRS, or command/handler pipelines · microservices · message queues or background
job infrastructure · vector databases · AI agent frameworks (Semantic Kernel, LangChain,
AutoGen) · a secrets manager.

**Do not violate:** one `AppDbContext`, injected directly into Application services ·
controllers stay thin, with business logic in Application services · domain entities free
of HTTP types, EF attributes, and infrastructure dependencies · external integrations
confined to Infrastructure · manual mapping only · expected failures returned as
`Result<T>`, unexpected failures left to `GlobalExceptionHandler` · one structured
prompt-response call per AI capability · no real secrets in committed files.

## Escalation

Stop and get explicit approval before:

- changing architecture, module boundaries, or layer responsibilities
- changing the database schema or adding a migration
- changing authentication or session behaviour
- changing an existing API response contract
- adding a new dependency
- moving files across layers or modules
- refactoring code unrelated to the task

When blocked, do not guess. State what is known, what is unknown, the options, a
recommendation, and the risk of each.

## Database Changes

Schema changes need approval before the migration is generated. When proposing one,
state why the change is required, the entity and its relationships, and the indexes and
constraints. Inspect the generated migration before applying it. A migration covers only
the tables the current task introduces.

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Verification

Run the relevant checks and report their actual output. Do not claim work is complete on
the basis that code was written.

```bash
dotnet build && dotnet test     # backend
npm run build && npm run lint   # frontend
```

A change is not done until: the checks pass or skipped checks are explained; tests cover
new service behaviour; new failure modes are mapped in `ResultMapper`; ownership checks
guard every path that reads user data; `CancellationToken` is threaded through async
service calls; and no guardrail above was violated.

If a check was not run, say so and say why.

## Anti-Patterns

Specific to this codebase:

- Business logic in controllers, or data access outside Application services.
- Raw `fetch()` in a component instead of the typed API client.
- Reading, storing, or decoding the JWT on the frontend — it is an `HttpOnly` cookie and
  auth state is server-authoritative.
- Treating every auth failure as logged-out, or trusting stale TanStack Query data after
  a 401.
- An async surface that does not handle loading, error, and empty states.
- Adding an abstraction before the duplication it removes actually exists.
- Generic or reusable components created before a second use case exists.
- Hardcoding prompt text in C# instead of a prompt file.
- Re-calling the AI provider for a result already persisted.
- Extending scope because adjacent code looked improvable.

## Out of Scope

Not built, and not scaffolded, until a task explicitly requires it:

**Product** — OAuth login, payments and subscription tiers, multi-tenancy, email
notifications, team or cohort views, saved search alerts, standalone mock interview.

**Engineering** — background job queues, `pgvector` or semantic search, per-environment
feature flags, A/B testing of prompt variants, a secrets manager, full CSRF token
implementation (`SameSite=Lax` is the accepted interim position), refresh tokens.

## Git

- Do not commit unless explicitly asked.
- Conventional commits, scoped to one task: `feat(resumes): add text extraction`.
- Branch from `develop`. `main` is production.
- Never commit secrets, local environment files, uploads, build outputs, or generated
  artifacts.
- Include migration files only when the task required a schema change.
