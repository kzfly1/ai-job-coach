# AI Job Coach — Project Context

> **For coding agents (Claude Code, Codex, Cursor, etc.)**
> Read this file before writing any code. Follow these rules exactly.
> When in doubt: do less, not more. Build only what the current ticket requires.

---

## Purpose

AI Job Coach helps software engineers discover relevant opportunities, improve application quality, prepare for interviews, and understand why they are not getting offers.

The product has two tracks:

**Track A — Application Copilot:** Help users get more interviews. Workflow: Resume → JD Analysis → Resume Match → Resume Suggestions → Tailored Resume → Apply.

**Track B — Career Intelligence:** Help users improve over time. Workflow: Applications → Interview Journal → AI Reflection → Career Insights Dashboard.

The goal is not to optimise ATS scores. The goal is to help software engineers get interviews and eventually receive offers.

---

## Current Status

**Sprint 1 complete. Sprint 2 planned — ready to start.**

Sprint 1 delivered: Auth (register, login, cookie auth), Resumes module (upload, list, get, soft delete), local file storage, dashboard shell, resume upload and list pages.

Sprint 2 focus: Resume Intelligence (text extraction + AI-generated Developer Profile) and JD Intelligence (structured Job Profile from pasted JD).

`LocalFileStorageService` remains active. Azure Blob Storage is **not** part of Sprint 2 — it is a Release Readiness task, planned after the core AI workflows are validated.

---

## Source of Truth

This file is the short coding-agent context. Deeper explanations live in:

- `docs/ARCHITECTURE.md` — module structure, layer responsibilities, controller style, data access, AI integration
- `docs/DECISIONS.md` — why each architectural decision was made and when to revisit it
- `docs/ROADMAP.md` — sprint goals, deliverables, and acceptance criteria for Sprints 2–8 and the Release Readiness milestone

---

## Tech Stack

| Layer | Stack |
|---|---|
| Backend | ASP.NET Core Web API, .NET 8, Controllers, EF Core 8, PostgreSQL (Npgsql), Serilog, FluentValidation |
| Frontend | Next.js App Router (latest stable), React (latest stable), TypeScript strict mode, Tailwind CSS (latest stable), shadcn/ui, TanStack Query v5, React Hook Form, Zod |
| Auth | Custom JWT (`System.IdentityModel.Tokens.Jwt`) + BCrypt (`BCrypt.Net-Next`, work factor 12) |
| Testing | xUnit, Moq, FluentAssertions, WebApplicationFactory |
| Deployment | Azure App Service, Azure Database for PostgreSQL, Azure Blob Storage, GitHub Actions |

---

## Architecture Rules

The backend is a **Modular Monolith** — one ASP.NET Core project, one deployable process. Modules are namespace-separated folders, not separate assemblies.

**Module structure** (every module follows this layout):
```
Modules/{ModuleName}/
  Domain/          # Entities only. No EF attributes, no HTTP types.
  Application/     # Services, DTOs, validators. Injects AppDbContext directly.
  Infrastructure/  # EF IEntityTypeConfiguration, file storage, external adapters.
  Controllers/     # Thin HTTP adapters. No business logic.
```

**Controller rules:**
- Use `[ApiController]`, `[Route("api/{resource}")]`, `[HttpPost]`, `[Authorize]`.
- Return `IActionResult`. Use `ResultMapper.ToActionResult()` to map `Result<T>`.
- Extract `userId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)` in the controller.
- Pass `userId` as a `Guid` parameter to services — services never receive `ClaimsPrincipal`.
- No business logic inside controllers.

---

## Current Repository Structure

```
src/
├── AIJobCoach.Api/
│   ├── Program.cs
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Migrations/
│   ├── Middleware/
│   │   └── GlobalExceptionHandler.cs
│   ├── Common/
│   │   └── Http/
│   │       └── ResultMapper.cs
│   └── Modules/
│       ├── Auth/
│       │   ├── Domain/User.cs
│       │   ├── Application/AuthService.cs, AuthDtos.cs, RegisterRequestValidator.cs
│       │   ├── Infrastructure/UserConfiguration.cs
│       │   └── Controllers/AuthController.cs
│       └── Resumes/                   # module plural; entity class singular
│           ├── Domain/Resume.cs
│           ├── Application/ResumeService.cs, ResumeDtos.cs, IFileStorageService.cs
│           ├── Infrastructure/ResumeConfiguration.cs, LocalFileStorageService.cs
│           └── Controllers/ResumeController.cs
└── AIJobCoach.SharedKernel/
    ├── Result.cs
    ├── PagedResult.cs
    └── Guard.cs

frontend/
├── app/
│   ├── (auth)/login/, (auth)/register/
│   └── (dashboard)/layout.tsx, dashboard/, resumes/
├── components/ui/, components/shared/
├── lib/api-client.ts, lib/auth.ts, lib/hooks/
└── types/api.ts
```

Do not create folders for `Matching/`, `JobSearch/`, `ResumeTailoring/`, `InterviewPreparation/`, `JobTracking/`, or `CareerInsights/` until their sprint begins. `JobDescriptions/` and `AI/` are Sprint 2 modules.

---

## Backend Rules

**AppDbContext:** One context at `Data/AppDbContext.cs`. Add `DbSet<T>` per entity. EF configurations are discovered automatically via `ApplyConfigurationsFromAssembly`.

**Services:** Inject `AppDbContext` and other dependencies directly via constructor. No `IResumeRepository` or `IUserRepository`. Return `Result<T>` for operations that can fail with a known business error.

**Result mapping:** All `Result<T>` to `IActionResult` translation goes through `ResultMapper.ToActionResult()`. Do not inline switch-on-error-code logic in controllers.

**Error responses:**
```
Business error:    { code: string, message: string }
Validation error:  { code: "VALIDATION_ERROR", message: "Validation failed.", errors: { [field]: string[] } }
Unexpected error:  { code: "INTERNAL_ERROR", message: "An unexpected error occurred.", traceId: string }
```

**Validation:** FluentValidation. Controllers manually invoke validators and return `422` on failure. No global validation filter in Sprint 1.

**Auth:**
- JWT secret minimum 32 chars. Validated at startup — app refuses to start if absent or too short.
- `POST /api/auth/register` and `POST /api/auth/login` are public.
- All other endpoints use `[Authorize]`.
- Duplicate email on register → `409`, code `EMAIL_ALREADY_EXISTS`.
- Invalid credentials → `401`, message `"Invalid email or password."` (never indicate which field failed).
- Token expiry: 24 hours. No refresh tokens in MVP.

**GlobalExceptionHandler:** Catches all unhandled exceptions. Logs at `Error` level with full stack trace. Returns `INTERNAL_ERROR` response. Never leaks `exception.Message` or stack trace to the client in non-Development.

---

## Resume Storage Rules

**Current — local storage (`LocalFileStorageService`):**
- `IFileStorageService` is the abstraction. `LocalFileStorageService` is the active implementation.
- Files saved to `FileStorage__LocalPath` (default `./uploads`).
- Storage path format: `{userId}/{guid}{extension}`. Never use the client-provided filename on disk.
- Accepted MIME types: `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
- Max file size: 5 MB. Validated in `ResumeService` before any I/O.
- Soft delete: `DELETE` sets `IsActive = false`. List and get filter on `IsActive = true`.

**Release Readiness — Azure Blob Storage:**
- `AzureBlobStorageService` will implement `IFileStorageService` and be activated via DI swap.
- `LocalFileStorageService` remains in Development; `AzureBlobStorageService` in non-Development.
- Azure Blob is **not** part of any numbered sprint — it is a Release Readiness task.
- No service or controller code changes are required when the swap happens.

---

## Frontend Rules

- TypeScript `strict: true`. No `any`. Ever.
- All API calls go through `lib/api-client.ts`. Never call `fetch()` directly in components.
- `apiClient` uses `credentials: 'include'` on every request. Do not manually read, store, or attach JWT tokens. The browser sends the `access_token` HttpOnly cookie automatically.
- Auth state managed by `useAuth()` hook (TanStack Query, `queryKey: ['auth', 'me']`), backed by `GET /api/auth/me`. The frontend never decodes the JWT.
- Server state via TanStack Query `useQuery` and `useMutation`. Server Actions only for simple profile updates.
- Forms: React Hook Form + Zod. Schemas colocated with the form component.
- Every async operation must handle loading, error, and empty states explicitly.
- All API response types defined in `types/api.ts`.

---

## Current Sprint Scope

**Sprint 2** is allowed to build:

- `ResumeTextExtractor` (PDF via PdfPig, DOCX via DocumentFormat.OpenXml).
- `AI` module: `IOpenAIClient`, `OpenAIClient`, `PromptLoader`, `resume-analysis.txt` and `jd-analysis.txt` prompt files.
- `ResumeAnalysis` entity, EF migration, `ResumeAnalysisService`, `POST /api/resumes/{id}/analyse`, `GET /api/resumes/{id}/analysis`.
- `JobDescriptions` module: `JobDescription` entity, `JobDescriptionService`, `JobDescriptionsController`. Endpoints: create (with immediate analysis), list, get detail, delete. No update/edit endpoint — JDs are created once and analysed; editing is not a Sprint 2 requirement.
- Developer Profile component and resume detail page (frontend).
- JD create and detail pages (frontend).
- Resume list analysis status badges (frontend).
- OpenAI timeout (30s), Polly retry (3 attempts, exponential backoff), structured AI call logging.

**Sprint 2** is **not** allowed to build:

- Azure Blob Storage (Release Readiness task — no sprint assignment).
- Resume Matching (Sprint 3).
- Job Search Integration (Sprint 4).
- Resume Tailoring (Sprint 5).
- Interview Preparation (Sprint 6).
- Application Tracking or Interview Journal (Sprint 7).
- Career Insights Dashboard (Sprint 8).
- Full CI/CD pipelines, Application Insights, security headers, health checks (Release Readiness).
- Any module folder not listed above.

---

## Hard Constraints

1. Build only what the current ticket requires. Do not add scope.
2. Do not create module folders before their sprint begins.
3. Use Controllers. Do not use Minimal API route groups for business endpoints.
4. One `AppDbContext`. No per-module DbContexts.
5. Inject `AppDbContext` directly in Application services. No repository interfaces.
6. Controllers must stay thin. All business logic belongs in Application services.
7. External adapters (storage, AI, email) belong in Infrastructure.
8. Manual mapping only. No AutoMapper.
9. No MediatR, CQRS, or command/handler patterns.
10. No microservices, message queues, or distributed systems.
11. No vector database (add `pgvector` only if semantic search is a proven requirement).
12. No agent frameworks (Semantic Kernel, LangChain, AutoGen). Direct `HttpClient` to OpenAI only.
13. Do not scaffold the `InterviewPreparation` module before Sprint 6. Mock interview as a standalone feature is not in scope — interview preparation (company brief + role-specific questions) is a Sprint 6 feature within the `InterviewPreparation` module.
14. No Azure Key Vault in MVP. Use Azure App Service environment variables.
15. No real secrets or credentials in committed files. Placeholders only.

---

## Git Workflow

```
main      ← production; protected; requires PR + CI + manual approval to deploy
develop   ← integration; protected; requires PR + CI; auto-deploys to staging on merge
feat/*    ← cut from develop, PR back to develop
```

Commit format (Conventional Commits):
```
feat(auth): implement JWT login endpoint
feat(resumes): add local file storage service
fix(auth): return generic message on invalid credentials
chore(deps): upgrade EF Core to 8.0.6
```

---

## Last Updated

- Sprint 1 complete. Sprint 2 in progress.
- Roadmap rewritten to reflect Roadmap v2 product direction: eight sprints across two product tracks (Application Copilot + Career Intelligence).
- Azure Blob Storage moved out of Sprint 2 — now a Release Readiness task with no fixed sprint assignment.
- Azure Blob, CI/CD, Application Insights, security headers, and health checks documented as a Release Readiness milestone, not sprint work.
- Module sprint assignments updated: `JobDescriptions/` and `AI/` are Sprint 2; `Matching/` is Sprint 3; `JobSearch/` Sprint 4; `ResumeTailoring/` Sprint 5; `InterviewPreparation/` Sprint 6; `JobTracking/` Sprint 7; `CareerInsights/` Sprint 8.
- Hard Constraint 13 updated: `InterviewPreparation` module is Sprint 6, not post-MVP.
- `LocalFileStorageService` remains active until Release Readiness.
