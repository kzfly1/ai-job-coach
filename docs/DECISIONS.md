# AI Job Coach — Technical Decision Records

Lightweight architectural decision records for the AI Job Coach MVP. Each entry documents a real engineering trade-off. Use this document to recall the reasoning behind key decisions and to articulate them clearly in technical interviews.

These records explain *why*. For what the resulting system looks like, see
[ARCHITECTURE.md](ARCHITECTURE.md). No record here schedules work or tracks progress.

---

## Decision Record Format

Each record uses five fields:

- **Status** — whether the decision is active, superseded, or under review
- **Context** — the problem or constraint that forced a choice
- **Decision** — what was chosen
- **Consequences** — what the decision buys and what it costs
- **Revisit When** — the concrete trigger for reconsidering

---

## Decision Records

---

### ADR-001: Use Modular Monolith for the MVP

**Status:** Accepted

**Context:** The project is built by a single developer in three months. A microservices architecture would require managing multiple deployable processes, inter-service networking, distributed tracing, and independent CI/CD pipelines — all overhead with no benefit at this scale.

**Decision:** Build the entire backend as a single ASP.NET Core Web API process. Separate concerns through module folders and namespace boundaries rather than through process boundaries.

**Consequences:** Deployment, debugging, and local development are significantly simpler. EF Core migrations, DI registration, and configuration all live in one place. The trade-off is that module boundaries are not enforced by the compiler — a developer could call across modules directly. This risk is acceptable given the team size is one. If individual modules need to scale independently in the future, the existing folder structure already mirrors what a separate service would look like, making extraction straightforward.

**Revisit When:** A specific module has clearly different scaling characteristics from the rest (for example, an AI inference module receiving 10x the traffic of the auth module), and horizontal scaling of the whole process is no longer cost-effective.

---

### ADR-002: Keep Modules Inside `AIJobCoach.Api`

**Status:** Accepted

**Context:** An alternative layout puts each module in its own class library project, enforcing boundaries through project references. This is architecturally cleaner but adds build complexity, multiple `.csproj` files, and project reference management — overhead that slows down a solo MVP.

**Decision:** All modules live under `src/AIJobCoach.Api/Modules/{ModuleName}/`. Boundaries are enforced by folder structure, namespaces, and code review rather than by the build system.

**Consequences:** The build stays simple — one API project, one shared kernel. EF migrations, `AppDbContext`, and DI all remain in a single project. The cost is that the compiler does not prevent cross-module imports. This is a reasonable trade-off for a solo project where there is no team enforcing discipline. Extracting a module to a separate assembly later is mechanical: the folder structure already maps one-to-one to what a separate project would look like.

**Revisit When:** The codebase grows large enough that accidental cross-module coupling becomes a real problem, or another developer joins and needs compile-time enforcement of module boundaries.

---

### ADR-003: Use ASP.NET Core Controllers for the API Layer

**Status:** Accepted

**Context:** ASP.NET Core offers two API styles: Minimal APIs (route groups, lambda handlers) and Controllers (`ControllerBase` subclasses). Minimal APIs have less ceremony but provide weaker tooling for attribute-based auth, model binding, and action filters. Controllers are more verbose but are the established pattern for structured APIs with authorisation, validation, and consistent response shaping.

**Decision:** Use `[ApiController]` controllers with `[Route]`, `[HttpPost]`, `[Authorize]`, and `IActionResult` return types. The `/health` endpoint remains a Minimal API since it has no auth, validation, or business logic.

**Consequences:** Controllers integrate cleanly with `[Authorize]`, FluentValidation, `ModelState`, and action filters. `IActionResult` return types make response shaping explicit. The trade-off is more boilerplate compared to Minimal APIs. Using both in the same project is valid — Minimal APIs for simple endpoints, controllers for anything with business logic.

**Revisit When:** A future version of Minimal APIs adds comparable support for filters and structured result types, making controllers redundant.

---

### ADR-004: Use One Shared `AppDbContext`

**Status:** Accepted

**Context:** A per-module `DbContext` pattern gives each module its own EF context, which is closer to true isolation. However, it adds complexity: multiple migration histories, multiple connection registrations, and no straightforward way to write queries that join across modules.

**Decision:** One `AppDbContext` at `AIJobCoach.Api/Data/AppDbContext.cs` contains all `DbSet<T>` declarations. Each module provides its own `IEntityTypeConfiguration<T>` class in its `Infrastructure/` layer, which is picked up by `ApplyConfigurationsFromAssembly`.

**Consequences:** Migrations are managed in one place. Cross-module queries (for example, joining applications with job descriptions) are straightforward. The trade-off is that the `AppDbContext` grows as modules are added — but for an MVP with five modules this is not a practical problem. True module isolation at the DB level (separate schemas or separate databases) is a post-MVP concern.

**Revisit When:** The team grows large enough that multiple developers are modifying the same `AppDbContext` and causing frequent migration conflicts, or a module has clearly different data storage requirements (for example, a caching module that would benefit from Redis).

---

### ADR-005: Allow Application Services to Inject `AppDbContext` Directly

**Status:** Accepted

**Context:** Clean Architecture prescribes that Application layer services depend only on interfaces defined in the Application layer, with Infrastructure providing concrete implementations. Strictly applied, this means services would depend on `IResumeRepository` and never directly on EF Core.

**Decision:** Application services inject `AppDbContext` directly. There is no `IResumeRepository` or `IUserRepository`.

**Consequences:** The Application layer has a compile-time dependency on EF Core. Swapping the data store in the future requires changing Application code, not just an Infrastructure binding. In practice, this is rarely done, and the simplicity gained is significant: no interface to maintain, no method-per-query proliferation, and full access to LINQ for composing queries. Services remain testable using EF Core's in-memory provider or a real test database.

**Revisit When:** A module's data access logic becomes complex enough that multiple services share non-trivial, reused queries. At that point, a focused query class inside `Infrastructure/` is the right abstraction — not a general-purpose repository interface.

---

### ADR-006: Avoid Repository Abstraction for Simple EF Core CRUD

**Status:** Accepted

**Context:** The repository pattern adds an interface between Application services and the data store. Its stated benefit is testability and data-store independence. In practice, most repository interfaces mirror EF Core's `DbSet<T>` API exactly, adding a layer with no additional semantics.

**Decision:** Do not create `IRepository<T>`, `IResumeRepository`, or similar abstractions in the MVP. Use `AppDbContext` and LINQ directly in service methods.

**Consequences:** Less code to write, read, and maintain. EF Core's full LINQ API is available without wrapping it. Tests use EF Core's in-memory provider or a real test database, both of which are straightforward. The trade-off — tighter coupling to EF Core — is accepted because swapping EF Core for a different ORM is not a realistic concern for this project.

**Revisit When:** A specific data access operation needs to be shared across multiple services and is complex enough that an abstraction adds clarity (not just a new interface wrapping the same query).

---

### ADR-007: Use Manual Mapping Instead of AutoMapper

**Status:** Accepted

**Context:** AutoMapper reduces boilerplate by mapping between types using convention-based rules. It adds a dependency, requires configuration profiles, and makes mapping logic invisible — errors like missing properties appear at runtime rather than at compile time.

**Decision:** Map between entities and DTOs manually using static `ToDto()` methods or constructors. No AutoMapper or equivalent library.

**Consequences:** Mapping code is explicit, easy to read, and type-checked at compile time. A renamed property causes a compiler error rather than a silent null. The trade-off is more code per DTO, but in practice each mapping is small and colocated with the DTO that uses it. For a project with five modules and limited DTO depth, the manual approach is consistently faster to reason about.

**Revisit When:** The project has many large, deeply nested DTOs where manual mapping becomes a maintenance burden and the risk of silent mapping errors is lower than the cost of writing boilerplate.

---

### ADR-008: Use Direct Service Calls Instead of MediatR/CQRS

**Status:** Accepted

**Context:** MediatR provides a mediator pattern where controllers dispatch commands and queries through a message bus to handlers. This decouples controllers from services and enables cross-cutting concerns like logging and validation via pipeline behaviours. It also adds indirection, more files per feature, and a non-trivial learning curve.

**Decision:** Controllers call Application services directly via constructor injection. No MediatR, no command objects, no handler pipeline.

**Consequences:** The call path from controller to service is explicit and navigable — pressing "go to definition" in any IDE jumps straight to the implementation. Debugging is straightforward. The trade-off is that cross-cutting concerns like logging and validation must be handled per-service or in middleware, rather than in a shared pipeline behaviour. For a solo project with five modules this is manageable.

**Revisit When:** Cross-cutting concerns (logging, validation, caching, authorisation checks) need to be applied consistently across many service methods and middleware cannot address them — at that point a pipeline behaviour pattern has genuine value.

---

### ADR-009: Use Custom JWT Authentication with BCrypt

**Status:** Accepted

**Context:** ASP.NET Identity provides a complete authentication system including user management, password hashing, roles, claims, and token generation. It also adds significant complexity: many generated tables, a rigid user model, and configuration overhead that is out of proportion for an MVP with a simple `users` table.

**Decision:** Implement authentication with a hand-written `AuthService`. Passwords hashed with BCrypt (work factor 12) via `BCrypt.Net-Next`. JWTs generated with `System.IdentityModel.Tokens.Jwt`. The JWT secret is validated at startup — the app refuses to start if the secret is absent or under 32 characters.

**Consequences:** Full control over the `User` entity, the token contents, and the auth flow. No unused Identity tables or configuration. The trade-off is that features ASP.NET Identity provides for free (password reset, email confirmation, lockout) must be built manually if needed. These are post-MVP concerns. Login error messages intentionally return a generic "Invalid email or password" response to prevent username enumeration.

**Revisit When:** The product requires password reset via email, account lockout, multi-factor authentication, or role management complex enough to justify adopting Identity.

---

### ADR-010: Store JWT in `localStorage` for MVP Frontend Auth

**Status:** Superseded by ADR-021

**Context:** The two options for client-side JWT storage are `localStorage` and `httpOnly` cookies. `httpOnly` cookies are inaccessible to JavaScript and therefore immune to XSS theft. `localStorage` is accessible to any JavaScript running on the page and is vulnerable to XSS attacks that steal the token.

**Decision:** ~~Store the JWT in `localStorage` for the MVP.~~ This decision was reversed before any release. See ADR-021.

**Consequences:** The original decision was made for implementation simplicity. It was reversed once it became clear that HttpOnly cookie auth could be implemented without significant added complexity, and that doing so removed a known security debt before any users touched the product.

**Revisit When:** N/A — superseded.

---

### ADR-011: Use Local File Storage in Development

**Status:** Accepted

**Context:** Resume file uploads require a storage backend. Cloud object storage is the production target, but depending on it during local development means an account, container provisioning, connection string management, and integration testing against a cloud service before any product feature is usable.

**Decision:** `LocalFileStorageService` saves files to a configured local directory and is the Development implementation. All file storage is accessed through an `IFileStorageService` interface so the implementation can be swapped without touching service or controller code.

**Consequences:** Local development has zero cloud dependencies — the API runs entirely against Docker Compose. The interface abstraction means introducing cloud storage is a new `Infrastructure/` class and a DI registration change. The trade-off is that local files are lost when the container is recreated and are not suitable for any deployed environment.

**Revisit When:** N/A — local storage is a Development-only implementation by design. See ADR-012 for the deployed case.

---

### ADR-012: Use Azure Blob Storage for Deployed Environments

**Status:** Accepted — amended

**Context:** Local file storage is not viable for staging or production. Files stored on an App Service instance are lost on restart or when the app is scaled. Azure Blob Storage provides durable, scalable object storage with per-environment container isolation.

**Decision:** `AzureBlobStorageService` implements `IFileStorageService` and is registered in non-Development environments; `LocalFileStorageService` remains active in Development. The swap is a DI registration conditional — no service or controller code changes.

**Consequences:** Cloud storage is in place before the first deployment, which is the correct order. Separate containers per environment prevent cross-environment data contamination. The trade-off is a cloud dependency in every deployed environment, requiring credential management.

**Amendment:** This decision was originally scheduled into a specific sprint. The schedule is withdrawn — the decision is *what* deployed environments use, not *when* it is built. It is a prerequisite of deploying, not of any particular feature.

**Revisit When:** A different cloud provider or self-hosted object storage (for example, MinIO) is preferred — the `IFileStorageService` interface supports swapping the implementation.

---

### ADR-013: Keep `content_text` Nullable

**Status:** Accepted

**Context:** Resume text extraction has real failure modes — scanned PDFs, encrypted files, malformed DOCX. A schema that assumes text is always present would force a choice between rejecting uploads that cannot be parsed and storing an empty string that is indistinguishable from a genuinely empty document.

**Decision:** `resumes.content_text` is a nullable `TEXT` column. `NULL` means "no text is available for this resume", and is a valid, expected state.

**Consequences:** The column is a truthful representation of the data. Upload and extraction stay separable concerns, and a resume whose text could not be extracted is still a valid record. The cost is that every consumer of `content_text` must handle the null case explicitly — which is the correct burden, since the condition is real.

**Revisit When:** Extraction becomes reliable enough that a null is always an error rather than an expected outcome — which would require OCR. See ADR-014.

---

### ADR-014: Extraction Failure Must Not Fail the Upload

**Status:** Accepted

**Context:** Resume analysis requires resume text as input. Extraction from PDF and DOCX uses different libraries (`PdfPig` and `DocumentFormat.OpenXml`), each with its own failure modes. The question is what happens to the upload when extraction fails.

**Decision:** Extraction is attempted during upload and its result stored in `content_text`. Failure sets `content_text` to `NULL`, logs a warning, and returns a successful upload. Analysis endpoints reject a resume with no text with a distinct business error rather than a generic failure.

**Consequences:** The extraction concern is cleanly separated from the storage concern. A resume can always be uploaded; analysis is conditionally available and the reason is surfaced to the user rather than degrading silently. The trade-off is that scanned PDFs, which require OCR, cannot be analysed.

**Revisit When:** A meaningful proportion of users upload scanned PDFs and cannot use analysis — at that point OCR (Azure Document Intelligence or Tesseract) should be evaluated.

---

### ADR-015: Use Direct OpenAI HTTP Integration

**Status:** Accepted

**Context:** Several frameworks abstract over LLM APIs: LangChain, Semantic Kernel, AutoGen. These provide tooling for agents, tool-calling chains, memory, and retrieval. They also add significant complexity, lock the codebase into framework-specific abstractions, and evolve rapidly in ways that create breaking changes.

**Decision:** Communicate with OpenAI using a named `HttpClient` registered in .NET's `IHttpClientFactory`. Prompts are sent as plain HTTP requests. Responses are parsed from JSON. No agent framework is used.

**Consequences:** The integration is simple, transparent, and easy to debug. Token usage and latency are logged directly. Changing to a different model or provider requires only changing the HTTP client configuration. The trade-off is that multi-step agentic workflows (tool calling, retrieval-augmented generation, memory) must be implemented manually. The MVP does not require any of these — every AI operation is a single-turn, structured prompt-response pair.

**Revisit When:** A product feature genuinely requires multi-step reasoning, tool invocation, or retrieval over a large corpus — and the complexity cost of the framework is lower than the cost of implementing the behaviour manually.

---

### ADR-016: Use Prompt Files for AI Prompts

**Status:** Accepted

**Context:** Embedding prompt text as C# string literals or string constants makes prompts hard to read, hard to iterate on, and invisible to anyone working outside the editor. Prompts are a distinct kind of content from code — they change frequently during development as output quality is tuned.

**Decision:** Prompts are stored as `.txt` files in `Modules/AI/Prompts/`. A `PromptLoader` service reads and caches them at startup and performs `{{placeholder}}` substitution. Prompt files are committed to source control.

**Consequences:** Prompts are readable as standalone documents, diffable in git, and editable without touching C# code. Prompt versioning is implicit through git history. The trade-off is that prompt changes require a redeployment. In the MVP this is acceptable — there is no runtime prompt management requirement.

**Revisit When:** Prompt iteration speed becomes a bottleneck and the team wants to modify prompts without a full deploy cycle — at that point a database-backed prompt registry with versioning becomes valuable.

---

### ADR-017: Bound the Interview Feature to Preparation, Not Simulation

**Status:** Accepted — amended

**Context:** "Interview support" can mean very different products. A mock interview — generating questions, accepting free-text answers, returning AI feedback — is a stateful, multi-turn interaction with its own UI surface and evaluation problem. Interview *preparation* — a company brief and a role-specific question bank — is a small number of single-turn generations. Company research can likewise expand without limit if it is treated as its own product.

**Decision:** The interview feature is preparation only: a lightweight company brief plus a question bank and preparation notes, each produced by a single structured generation. Mock interview simulation is out of scope. Company intelligence is a sub-feature that feeds question generation — not a standalone research product — and does not call external company-data APIs.

**Consequences:** The interview surface stays proportionate to its value, and the core loop (resume analysis and matching) is delivered completely rather than thinly across more features. Nothing stateful or multi-turn enters the AI layer, which keeps ADR-015's single-call rule intact. The trade-off is that users get preparation material rather than practice.

**Amendment:** This record originally asserted a fixed three-method AI interface and a specific sprint timeline. Both are withdrawn. The implemented AI seam is a low-level transport client (see ADR-015 and `docs/ARCHITECTURE.md`), and scheduling is not the business of an architectural decision. The scope boundary above is the durable part.

**Revisit When:** The core loop is shipped and validated, and practice — rather than preparation — is the constraint users actually report.

---

### ADR-018: Use the Latest Stable Frontend Stack

**Status:** Accepted

**Context:** The frontend stack choices affect both development velocity and portfolio signal. Older or less-used choices (Create React App, class components, Redux) are a weaker portfolio signal for 2025 roles than the current mainstream stack.

**Decision:** Next.js App Router, TypeScript strict mode, Tailwind CSS, shadcn/ui, TanStack Query v5, React Hook Form, and Zod. All at latest stable versions at the time of project start.

**Consequences:** The stack aligns with current industry practice for React-based frontends. TanStack Query v5 handles server state management cleanly without the boilerplate of Redux. shadcn/ui provides accessible, customisable components without a heavy component library dependency. The trade-off is that cutting-edge versions occasionally have rough edges and thinner community documentation than older stable versions.

**Revisit When:** A specific library introduces a breaking change in a major version update — upgrade on a per-library basis as needed.

---

### ADR-019: Use Azure App Service Environment Variables for MVP Secrets

**Status:** Accepted

**Context:** Azure Key Vault provides centralised secret management with access policies, audit logging, and automatic rotation. It is the correct long-term solution for secret management. It also requires additional Azure resources, managed identities, and configuration overhead.

**Decision:** All secrets (database connection string, JWT secret, OpenAI API key, Blob Storage connection string) are stored as Azure App Service environment variables. Key Vault is explicitly deferred to post-MVP.

**Consequences:** Secrets are managed through the Azure Portal or Azure CLI with no additional infrastructure. Environment variables are available to the application at runtime via the standard .NET configuration system. The trade-off is that secrets are not centrally audited and rotation is manual. This is acceptable for a solo portfolio project with no production users. Azure Key Vault is documented as a post-MVP obligation.

**Revisit When:** A second developer joins the project, the application is opened to real users, or a security audit identifies the manual rotation process as a risk.

---

### ADR-020: Weave Production Hardening Into Feature Work

**Status:** Accepted — supersedes an earlier schedule-based version

**Context:** Production concerns — timeouts, retries, rate limiting, structured logging, config validation — can be handled two ways: deferred into a dedicated hardening phase, or built alongside the feature that needs them. Deferral is tempting because it keeps feature work moving, but it accumulates a body of work whose scope is only discovered at the end, and it leaves every feature unprotected in the meantime.

**Decision:** Production-readiness work that belongs to a feature ships with that feature. When an integration is introduced, its timeout, retry policy, failure mapping, and structured logging are part of the same change. When an expensive endpoint is introduced, its rate limit is part of the same change. Configuration an environment cannot run without is validated at startup by the change that introduces it.

Infrastructure that is not attached to any single feature — deployment topology, CI/CD pipelines, observability wiring, TLS, security headers — is a prerequisite of deploying rather than of any feature, and is defined as a requirement rather than scheduled against feature work. Those requirements live in `docs/ARCHITECTURE.md`.

**Consequences:** No feature reaches a deployed environment in an unhardened state, and there is no end-of-project hardening backlog of unknown size. The cost is that every feature change is slightly larger than its happy path. The trade-off is deliberate: a timeout added with the integration is a line of configuration, while a timeout added six features later is an audit.

**Amendment:** This record originally expressed the same intent as a fixed sprint schedule that no longer exists. The schedule is withdrawn; the principle is what was always durable.

**Revisit When:** A hardening concern is genuinely cross-cutting and cannot be attached to any single feature — at which point it belongs in the production requirements rather than in a feature change.

---

### ADR-021: Use HttpOnly Cookie Authentication

**Status:** Accepted

**Context:** The original plan stored the JWT in `localStorage` and injected it as an `Authorization: Bearer` header. This approach is straightforward but exposes the token to XSS: any JavaScript running on the page can read `localStorage`. The alternative — an `HttpOnly` cookie — is invisible to JavaScript entirely. The cookie is set and cleared by the server, and the browser sends it automatically with every credentialed request.

**Decision:** The backend sets the JWT in an `HttpOnly` cookie named `access_token` on successful login and register. The frontend never reads, stores, or decodes the token. `apiClient` uses `credentials: 'include'` on every request. `GET /api/auth/me` is the sole source of truth for the current user — `useAuth()` calls it via TanStack Query (`queryKey: ['auth', 'me']`) to hydrate user state. Logout calls `POST /api/auth/logout`, which clears the cookie server-side.

**Consequences:** The JWT is never accessible to JavaScript, eliminating the XSS token-theft vector. The frontend is simpler — no token storage, no manual header injection, no JWT decoding. Auth state is always server-authoritative via `/api/auth/me`. The trade-offs are: (1) cookie auth introduces CSRF considerations — mitigated by `SameSite=Lax` for the MVP, with full CSRF hardening required before public launch; (2) local development requires consistent protocol use (HTTP on both frontend and backend, or HTTPS on both) to avoid browser cookie/CORS issues from mixed schemes; (3) Bearer token support via the `Authorization` header may remain available for Postman and development tooling, but the frontend must not use it.

**Cookie configuration:**
- Name: `access_token`
- `HttpOnly: true`
- `Secure: true` in non-Development; `false` in Development
- `SameSite: Lax`
- `Path: /`
- Expiry matches JWT expiry (24 hours)

**Deferred:** Refresh tokens (post-MVP). Full CSRF token implementation (required before public launch). `SameSite=Lax` is acceptable for the current MVP setup.

**Revisit When:** Full CSRF protection is needed before public launch. Refresh tokens are needed for longer session lifetimes.

---

## Summary of Revisit Triggers

The following conditions should prompt a review of one or more decisions above:

| Trigger | Relevant ADRs |
|---|---|
| A second developer joins the project | ADR-002, ADR-006, ADR-019 |
| The application is opened to real users | ADR-019, ADR-021 |
| A module needs to scale independently | ADR-001 |
| Cross-module coupling becomes measurable and problematic | ADR-002, ADR-006 |
| Scanned PDF uploads become a common user complaint | ADR-014 |
| Prompt iteration speed blocks feature development | ADR-016 |
| An AI feature requires multi-step reasoning or retrieval | ADR-015 |
| Interview practice, not preparation, becomes the reported constraint | ADR-017 |
| Full CSRF protection is needed before public launch | ADR-021 |
| Refresh tokens are needed for longer session lifetimes | ADR-021 |
| Manual secret rotation causes an incident | ADR-019 |
| A hardening concern cannot be attached to any single feature | ADR-020 |

---

*Add a new ADR when a significant architectural decision is made. Do not delete
superseded or amended ADRs — update their status and reference what replaced them.
Record decisions and their reasoning here; record implementation state nowhere.*
