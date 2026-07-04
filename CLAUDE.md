# Claude Instructions — AI Job Coach

## Read First

Before making code changes, read:

* `PROJECT_CONTEXT.md`

Treat `PROJECT_CONTEXT.md` as the primary source of truth for:

* current sprint scope
* architecture rules
* module boundaries
* backend/frontend conventions
* current implementation priorities
* prohibited patterns

Read these additional docs only when relevant:

* `docs/ARCHITECTURE.md` — when changing project structure, module structure, layers, dependency direction, or integration boundaries
* `docs/DECISIONS.md` — when a ticket involves technical trade-offs or competing implementation options
* `docs/ROADMAP.md` — when a ticket affects sprint planning, feature sequencing, or future scope
* `docs/AI_WORKFLOW.md` — when deciding how to classify, plan, implement, review, and verify a coding task

## Working Rules

* Work on one ticket at a time.
* Implement only the requested ticket scope.
* Prefer small, focused changes over broad rewrites.
* Preserve existing project conventions.
* Build vertical slices when the ticket requires a feature.
* Do not scaffold future modules before their sprint.
* Do not create empty placeholder folders.
* Ask for confirmation before changing architecture, database schema, authentication, deployment, or shared conventions.

## Backend Rules

* Use ASP.NET Core Web API Controllers.
* Use `ControllerBase` for API controllers.
* Keep Controllers thin: bind request, extract user/claims, call Application service, return response.
* Put business workflow in Application services.
* Put external integrations in Infrastructure.
* Keep Domain entities free of HTTP types, EF attributes, and infrastructure dependencies.
* Use one shared `AppDbContext`.
* Application services may inject `AppDbContext` directly.
* Use EF Core entity configurations in Infrastructure.
* Use manual mapping.
* Keep expected failures explicit through service results or controller responses.
* Let `GlobalExceptionHandler` handle unexpected failures.

## Frontend Rules

* Use the latest stable frontend stack defined in `PROJECT_CONTEXT.md`.
* Use TypeScript strict mode.
* Use typed API client wrappers instead of raw `fetch()` inside components.
* Use TanStack Query for server state.
* Use React Hook Form and Zod for forms.
* Keep page components thin.
* Move reusable logic into components, hooks, or API utilities.
* Handle loading, error, and empty states for async UI.

## MVP Constraints

* Do not introduce repository pattern.
* Do not introduce AutoMapper.
* Do not introduce MediatR or CQRS.
* Do not introduce microservices.
* Do not introduce message queues.
* Do not introduce vector databases.
* Do not introduce agent frameworks.
* Do not scaffold Mock Interview during MVP.
* Do not create Azure Blob implementation before the sprint that requires it.
* Do not commit secrets, local environment files, uploaded files, build outputs, or generated artifacts.

## Before Editing Code

For every ticket, first respond with:

1. Understanding of the ticket
2. Relevant files to inspect
3. Implementation plan
4. Files expected to change
5. Questions, risks, or assumptions

Wait for approval before making broad structural changes.

## After Editing Code

Run the relevant checks.

For backend changes:

```bash
dotnet build
dotnet test
```

For frontend changes:

```bash
npm run build
npm run lint
```

For database schema changes:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

Only run EF migration commands when the ticket explicitly requires a schema change.

## Git Rules

* Do not commit unless explicitly asked.
* Use conventional commit messages when asked to commit.
* Keep commits focused on one ticket.
* Do not include unrelated formatting changes.
* Do not include generated local files, secrets, uploads, build artifacts, or environment-specific files.

## Communication Style

When starting a ticket, provide:

1. Ticket understanding
2. Short implementation plan
3. Files to inspect or modify
4. Risks or questions

When finishing a ticket, provide:

1. What changed
2. How it was tested
3. Any trade-offs
4. Follow-up tasks, if any
