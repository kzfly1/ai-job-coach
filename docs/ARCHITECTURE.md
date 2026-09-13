# AI Job Coach — Architecture

Durable system shape and engineering conventions.

This document describes how the system is structured and why. It does not describe
what is currently built or what is planned — inspect the repository for the former,
and the current task's scope for the latter.

For the reasoning behind specific trade-offs, see [DECISIONS.md](DECISIONS.md).
For agent working rules, see [../CLAUDE.md](../CLAUDE.md).

---

## System Shape

A Next.js App Router frontend and a single ASP.NET Core Web API communicating over
HTTPS/JSON. No BFF, no API gateway, no microservices.

```
Next.js frontend
      │  HTTPS / JSON  (HttpOnly cookie auth)
ASP.NET Core Web API  ── modular monolith, one deployable process
      │
      ├── PostgreSQL          (EF Core / Npgsql)
      ├── File storage        (IFileStorageService seam)
      └── OpenAI              (direct HTTP, no agent framework)
```

**Stack.** Backend: ASP.NET Core Web API on .NET 8, EF Core 8, PostgreSQL, Serilog,
FluentValidation. Frontend: Next.js App Router, TypeScript strict mode, Tailwind,
shadcn/ui, TanStack Query v5, React Hook Form, Zod. Auth: custom JWT + BCrypt.
Testing: xUnit with FluentAssertions.

---

## Backend

### Modular Monolith

One deployable API project. Modules are namespace-separated folders inside it, not
separate assemblies. Boundaries are enforced by folder structure, namespaces, and
review — not by the compiler. The folder layout deliberately mirrors what a separate
project would look like, so extracting a module later is mechanical.

A separate `SharedKernel` project holds `Result<T>`, `Error`, `PagedResult<T>`, and
`Guard`. It depends on nothing else in the solution and never absorbs HTTP concerns.

### Module Layout

Every module follows the same four-layer structure:

```
Modules/{ModuleName}/
  Domain/          entities only
  Application/     services, DTOs, validators, owned interfaces
  Infrastructure/  EF configurations, external adapters
  Controllers/     thin HTTP adapters
```

Module folders are plural; entity classes are singular (`Modules/Resumes/` contains
`Resume`). This avoids a namespace collision between the module and its entity, and
applies to every module where the two names would otherwise match.

Create a module folder only when a task requires it. Empty or placeholder modules are
not created in advance.

### Layer Responsibilities

**Domain** — entity classes and their business state and behaviour. No EF attributes,
no DTOs, no HTTP types, no infrastructure dependencies. Entities protect their
invariants: prefer private setters with constructors or factory methods over public
mutable properties.

**Application** — business workflow. Services, DTOs, FluentValidation validators, and
any interface the module owns (for example, a storage or extraction seam). Application
services inject `AppDbContext` directly. They never see `HttpContext`, `ClaimsPrincipal`,
or `IActionResult`; a caller's identity arrives as a plain `Guid` parameter. Operations
that can fail with a known business error return `Result<T>`.

**Infrastructure** — `IEntityTypeConfiguration<T>` classes and adapters to anything
external: file storage, HTTP clients, document parsers.

**Controllers** — receive the request, extract identity from JWT claims, call one
Application service, map the result. Nothing else. No business logic, no data access.

### Controller Conventions

- `[ApiController]` with a class-level `[Route("api/{resource}")]`.
- Return `IActionResult`, not `IResult`.
- Extract the user id from `User.FindFirstValue(ClaimTypes.NameIdentifier)` and pass it
  to the service as a `Guid`.
- `[Authorize]` by default; public endpoints are the explicit exception.
- Validators are invoked explicitly and return `422` on failure. There is no global
  validation filter.
- Translate `Result<T>` through the shared `ResultMapper` extension on `ControllerBase`.
  Controllers never switch on error codes themselves; a new business error code is added
  to `ResultMapper` and nowhere else.

Business endpoints use controllers. Minimal APIs are reserved for endpoints with no
auth, validation, or business logic — the health endpoint is the one current example.

### Error Contract

Three response shapes, and only three:

```
business    { code, message }
validation  { code: "VALIDATION_ERROR", message, errors: { field: string[] } }
unexpected  { code: "INTERNAL_ERROR", message, traceId }
```

`GlobalExceptionHandler` implements `IExceptionHandler`, logs the full exception at
`Error` level, and returns the unexpected shape. It never leaks an exception message or
stack trace outside Development.

### Data Access

One `AppDbContext` holds every `DbSet<T>`. Application services inject it directly —
there are no repository interfaces. EF configurations are discovered automatically via
`ApplyConfigurationsFromAssembly`, so a new module's configuration needs no registration.

When several services come to share one non-trivial query, extract a focused query class
into that module's `Infrastructure/` layer. That is not a repository, and it is the only
sanctioned abstraction over `DbContext`.

Migrations live in the API project and are added only when a task requires a schema
change. A migration covers the tables that task introduces — never tables for work that
has not started.

### Registration

Services are registered in `Program.cs` with `AddScoped`. Controllers are discovered by
`MapControllers()` and EF configurations by `ApplyConfigurationsFromAssembly`, so
neither needs per-module wiring. There is no module registration abstraction.

Configuration that the application cannot run without — the JWT secret, the OpenAI API
key outside Development — is validated at startup, and the application refuses to start
if it is absent or malformed. Secrets live in environment variables, never in committed
files.

---

## Integration Seams

The system has exactly two seams to the outside world. Both are interfaces owned by the
Application layer and implemented in Infrastructure.

### File Storage

`IFileStorageService` abstracts resume and document storage behind `SaveAsync` and
`DeleteAsync`. A local-disk implementation serves Development; a cloud implementation is
selected by environment in DI. Swapping implementations must require no change to any
service or controller.

Stored files are named from a generated identifier scoped by user — a client-supplied
filename is never used as a path on disk. Uploads are constrained by an allowed MIME
type list and a maximum size, both checked before any I/O.

Deletion is soft: the record is flagged inactive and queries filter on it. The stored
file is retained, which keeps the database and the storage backend from needing a
coordinated two-phase delete.

### OpenAI

`IOpenAIClient` is a deliberately low-level transport seam: it takes a system prompt and
a user prompt and returns the raw model content. Building prompts and parsing responses
into typed shapes belongs to the calling Application service, not to the client.

Higher-level capabilities compose on top of it. Each AI capability is **one structured
prompt-response call** — no agent orchestration, no tool calling, no multi-step chains.
A capability that cannot be expressed as a single call is a signal to revisit the design,
not to add a framework.

The client is a named `HttpClient` with an explicit timeout and a Polly retry policy for
transient errors. A malformed response or an envelope with no content raises a dedicated
parse exception carrying the raw response, so failures are diagnosable from logs alone.

AI calls are synchronous from the request's perspective: the controller waits. If latency
becomes a problem, a background worker with a polling endpoint can be introduced behind
the same interface.

### Prompt Management

Prompts are plain text files under the AI module's `Prompts/` folder, loaded and cached
by `PromptLoader`, which performs `{{placeholder}}` substitution. Prompts are never C#
string literals — they are content, they change independently of code, and they must be
diffable.

Every prompt embeds its expected JSON schema and instructs the model to return valid JSON
only, with no markdown fences and no surrounding prose. The schema is never left for the
model to infer.

### AI Cost Controls

- A small, configurable default model.
- Token usage and latency logged as structured properties on every call — discrete
  Serilog properties, not interpolated strings.
- AI results are persisted. Re-running an identical operation returns the stored result
  and must not trigger a second call to the provider. Where a capability genuinely needs
  to re-run — a user rejecting a profile as wrong — it takes an explicit `?force=true`
  query parameter, and that is the only path that spends a second call.
- AI endpoints are rate limited per user. The more expensive an operation, the tighter
  its limit.

---

## Frontend

### Structure

Route groups separate public routes from authenticated ones. The authenticated group's
layout owns the redirect guard: it reads auth state, renders a skeleton while loading to
prevent a flash of protected content, and redirects when there is no user. Auth is
guarded client-side; there is no Next.js middleware in the auth path.

Page components stay thin — they read params, call hooks, and pass data down. Display and
interaction logic lives in feature components. Data fetching and mutation logic lives in
hooks, not in components.

### API Access

Every HTTP call goes through the typed API client. No component calls `fetch()` directly.
The client sends credentials on every request, so the JWT — held in an `HttpOnly` cookie
— is attached by the browser. The frontend never reads, stores, decodes, or attaches the
token itself.

Auth state is server-authoritative: a single TanStack Query hook backed by the current-user
endpoint is the only source of truth for who is signed in. Logout clears the cookie
server-side and then clears the cached query.

All API response types are declared in one types module and mirror the backend DTOs.
TypeScript runs in strict mode; `any` is not acceptable.

### State and Forms

Server state is TanStack Query — `useQuery` for reads, `useMutation` with explicit cache
invalidation for writes. Server Actions are reserved for trivial updates that need no
loading state or invalidation; uploads and AI-triggered operations always use mutations.

Forms use React Hook Form with Zod schemas colocated with the form component.

Every async surface handles three states explicitly: loading (a skeleton for lists, not a
spinner), error, and empty (a useful prompt with an action, not a blank panel).

---

## Testing

**Unit tests** target the Application layer and pure helpers: service business logic,
ownership checks, validators, result mapping, prompt loading and substitution, document
text extraction, and client retry and parse-failure behaviour.

**Integration tests** belong at the endpoint level and should use `WebApplicationFactory`
against a real test database, covering per endpoint: the happy path, auth failure,
validation failure, and not-found — including the case where a resource belongs to another
user. Controllers are covered this way rather than by mocked unit tests.

Adapters with real side effects — file storage in particular — are tested against the real
dependency rather than a mock. Prefer a real or in-memory database over mocking
`AppDbContext`.

Any test that calls a genuinely external service (the AI provider, cloud storage) must be
attributed with a category so it can be excluded from the standard run. Use the category
in the filter when such tests exist:

```bash
dotnet test --filter "Category!=ManualOnly&Category!=AIIntegration&Category!=AzureIntegration"
```

---

## Production Requirements

These are requirements for a production-ready deployment, not a schedule.

**Topology.** App Service, a managed PostgreSQL instance, and a storage container per
environment, with containers isolated per environment so data cannot cross between them.
A staging slot enables blue/green swap. HTTPS is enforced and HTTP redirected.

**Health.** A liveness endpoint that reports only that the process is up, and a readiness
endpoint that checks reachability of PostgreSQL, storage, and the AI provider, returning
`503` with per-dependency status when any check fails.

**Observability.** Serilog ships to Application Insights. Every request is logged with
method, path, status, and duration. Every AI call is logged as a dependency trace with
latency and token count.

**Security.** CORS locked to the known frontend origin via configuration.
`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, and HSTS set on responses.
All secrets supplied as environment variables.

---

## Extending the System

**A new module.** Create only the layers the task needs. Add the entity's `DbSet` to
`AppDbContext`, add a migration covering only the new tables, register the service in
`Program.cs`, then build outward: domain → application → infrastructure → controller →
frontend client → hook → UI → tests. Controllers and EF configurations need no
registration.

**A new business error code.** Return it from the service in a `Result<T>`, then add the
single mapping in `ResultMapper`. Nowhere else.

**A new AI capability.** Add a prompt file with its schema embedded, add an Application
service that injects `IOpenAIClient`, render the prompt, make one call, parse the result
into a typed shape. Persist the result so a repeat request does not re-call the provider.

**A new dependency.** Not without explicit approval — see [../CLAUDE.md](../CLAUDE.md).
