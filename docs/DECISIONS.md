# AI Job Coach — Technical Decision Records

Lightweight architectural decision records for the AI Job Coach MVP. Each entry documents a real engineering trade-off. Use this document to recall the reasoning behind key decisions and to articulate them clearly in technical interviews.

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

**Status:** Accepted (technical debt acknowledged)

**Context:** The two options for client-side JWT storage are `localStorage` and `httpOnly` cookies. `httpOnly` cookies are inaccessible to JavaScript and therefore immune to XSS theft. `localStorage` is accessible to any JavaScript running on the page and is vulnerable to XSS attacks that steal the token.

**Decision:** Store the JWT in `localStorage` for the MVP. This is explicitly logged as technical debt.

**Consequences:** Simpler frontend implementation — no server-side cookie management, no CSRF token handling, and no coordination between the Next.js App Router and the API on cookie configuration. The security trade-off is real: an XSS vulnerability in the frontend could expose the token. This is acceptable for an MVP with no real user data and no production traffic. Moving to `httpOnly` cookies before a public launch is a documented obligation.

**Revisit When:** The application is opened to real users, particularly before any feature that handles personal or sensitive data beyond what is currently stored.

---

### ADR-011: Use Local File Storage in Sprint 1

**Status:** Accepted

**Context:** Resume file uploads require a storage backend. Azure Blob Storage is the production target, but setting it up in Sprint 1 would require an Azure account, container provisioning, connection string management, and integration testing against a cloud service before any product features exist.

**Decision:** Sprint 1 uses a `LocalFileStorageService` that saves files to a configured local directory. All file storage is accessed through an `IFileStorageService` interface so the implementation can be swapped without touching service or controller code.

**Consequences:** Zero cloud dependencies in Sprint 1 — the API runs entirely with Docker Compose. The interface abstraction means Sprint 2's Azure Blob introduction is a new `Infrastructure/` class and a one-line DI registration change. The trade-off is that local files are lost if the container restarts and are not suitable for production.

**Revisit When:** N/A — local storage is replaced by design in Sprint 2 before any staging deployment.

---

### ADR-012: Replace Local File Storage with Azure Blob Storage in Sprint 2

**Status:** Accepted

**Context:** Local file storage is not viable for staging or production. Files stored on an App Service instance are lost on restart or when the app is scaled. Azure Blob Storage provides durable, scalable object storage with per-environment container isolation.

**Decision:** `AzureBlobStorageService` implements `IFileStorageService` and is registered in non-Development environments. Sprint 1's `LocalFileStorageService` remains active in Development. The swap is a DI registration conditional — no service or controller code changes.

**Consequences:** Cloud storage is introduced before the first staging deployment, which is the correct order. Separate containers per environment (`resumes-dev`, `resumes-staging`, `resumes-prod`) prevent cross-environment data contamination. The trade-off is an Azure dependency from Sprint 2 onward, requiring the developer to manage Blob Storage credentials.

**Revisit When:** A different cloud provider or self-hosted object storage (for example, MinIO) is preferred — the `IFileStorageService` interface supports swapping the implementation.

---

### ADR-013: Keep `content_text` Nullable in Sprint 1

**Status:** Accepted

**Context:** Resume text extraction (PDF, DOCX parsing) is a non-trivial feature that introduces library dependencies and failure modes. Bundling it with the initial upload endpoint in Sprint 1 would add scope and risk to a ticket that is already establishing the file storage pattern, the multipart upload flow, and the DB schema.

**Decision:** The `resumes.content_text` column exists in the Sprint 1 migration as a nullable `TEXT` column. All Sprint 1 uploads set it to `NULL`. Extraction is added in Sprint 2 as an incremental change to `ResumeService`.

**Consequences:** Sprint 1 upload is simpler to implement and test. The nullable column is a truthful representation of the data state — text may not be available for a given resume. Sprint 2's extraction feature only needs to update `content_text` on existing rows; no schema migration is required. The trade-off is that Sprint 1 resumes cannot be analysed until Sprint 2 runs extraction.

**Revisit When:** N/A — this is a sprint boundary decision that resolves itself in Sprint 2.

---

### ADR-014: Add Text Extraction in Sprint 2

**Status:** Accepted

**Context:** Resume analysis requires the resume text as input to the AI. Text extraction from PDF and DOCX files uses different libraries (`PdfPig` and `DocumentFormat.OpenXml`), both of which have their own failure modes (scanned PDFs, encrypted files, malformed DOCX). Extraction failure should not prevent upload.

**Decision:** Sprint 2 adds `ResumeTextExtractor` to the Resumes module. On upload, extraction is attempted and the result stored in `content_text`. Extraction failure sets `content_text` to `NULL`, logs a warning, and does not fail the upload. AI analysis endpoints return `422` if `content_text` is null.

**Consequences:** The extraction concern is cleanly separated from the storage concern. A resume can always be uploaded; analysis is conditionally available. The failure mode is surfaced clearly to the user rather than silently degrading. The trade-off is that scanned PDFs (which require OCR) will not be analysable in the MVP.

**Revisit When:** A meaningful proportion of users upload scanned PDFs and cannot use the analysis feature — at that point, OCR (Azure Document Intelligence or Tesseract) should be evaluated.

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

### ADR-017: Keep Mock Interview Out of the MVP

**Status:** Accepted

**Context:** Mock interview was included in the original product spec. It requires generating interview questions from a JD and resume, accepting free-text answers, and returning AI-generated feedback — all stateful, multi-step interactions with distinct UI requirements.

**Decision:** Mock interview is post-MVP. No interview-related database tables, API endpoints, prompt files, or UI are created. The AI interface defines exactly three methods: `AnalyzeJobDescriptionAsync`, `AnalyzeResumeAsync`, and `MatchResumeToJDAsync`.

**Consequences:** Sprint 4 and the end of Sprint 3 recover approximately two weeks that would have been spent on interview scaffolding. Those weeks are redirected to job tracking, dashboard, and production hardening — all of which deliver more portfolio signal than a partially-built interview feature. The core value proposition (resume analysis + matching) is delivered completely rather than thinly across more features.

**Revisit When:** The core MVP is shipped, validated, and the matching + tracking features are demonstrated to work reliably in production.

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

### ADR-020: Defer Advanced Production Hardening to Later Sprints

**Status:** Accepted

**Context:** Production-grade observability (Application Insights, structured telemetry, alerts), security hardening (rate limiting, security headers, CORS locking), CI/CD pipelines, and health checks all take real time to implement correctly. Doing all of this in Sprint 1 would mean shipping no product features.

**Decision:** Sprints 1–4 focus on product features. Sprint 5 delivers Azure deployment and CI/CD. Sprint 6 delivers observability, security headers, rate limiting, health checks, and production sign-off. Each production engineering concern is a first-class sprint deliverable, not an afterthought.

**Consequences:** The MVP reaches a feature-complete state by Sprint 4 and a production-ready state by Sprint 6. Separating product sprints from infrastructure sprints makes each sprint's goal clear and achievable. The trade-off is that the staging environment is not fully hardened until Sprint 6 — acceptable given there are no real users at that point. Treating production readiness as dedicated sprint work also makes it a more credible portfolio demonstration than if it were crammed into existing tickets.

**Revisit When:** A specific security or reliability concern is identified during Sprint 1–4 that cannot wait — for example, a dependency with a critical CVE that CI would have caught.

---

## Summary of Revisit Triggers

The following conditions should prompt a review of one or more decisions above:

| Trigger | Relevant ADRs |
|---|---|
| A second developer joins the project | ADR-002, ADR-006, ADR-019 |
| The application is opened to real users | ADR-010, ADR-019 |
| A module needs to scale independently | ADR-001 |
| Cross-module coupling becomes measurable and problematic | ADR-002, ADR-006 |
| Scanned PDF uploads become a common user complaint | ADR-014 |
| Prompt iteration speed blocks feature development | ADR-016 |
| An AI feature requires multi-step reasoning or retrieval | ADR-015 |
| Mock interview is prioritised as the next product feature | ADR-017 |
| A security audit identifies JWT storage as a live risk | ADR-010 |
| Manual secret rotation causes an incident | ADR-019 |

---

*Last updated: Sprint 1 — initial decision record.*
*Add a new ADR when a significant architectural or product decision is made. Do not delete superseded ADRs — update their status to "Superseded" and reference the replacement.*
