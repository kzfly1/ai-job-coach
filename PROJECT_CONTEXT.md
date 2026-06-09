# AI Job Coach — Project Context

> **For coding agents (Claude Code, Codex, Cursor, etc.)**
> Read this file before writing any code. Follow these rules exactly.
> When in doubt: do less, not more. Build only what the current ticket requires.

---

## Purpose

AI Job Coach helps early-career developers close the gap between their current profile and job market expectations. Users upload resumes, analyse job descriptions, get match scores and skill gap feedback, and track applications. This is a production-deployed portfolio project demonstrating full-stack and AI engineering.

---

## Current Status

**Sprint 1 in progress.** Auth and Resumes modules are being built. Local file storage is active. No AI calls, no Azure Blob, no text extraction yet.

---

## Source of Truth

This file is the short coding-agent context. Deeper explanations live in:

- `docs/ARCHITECTURE.md` — module structure, layer responsibilities, controller style, data access, AI integration
- `docs/DECISIONS.md` — why each architectural decision was made and when to revisit it
- `docs/ROADMAP.md` — sprint goals, deliverables, and acceptance criteria for Sprints 2–6

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

Do not create folders for `JobDescriptions/`, `Matching/`, `JobTracking/`, or `AI/` until their sprint begins.

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

**Sprint 1 — local storage:**
- `IFileStorageService` is the abstraction. `LocalFileStorageService` is the Sprint 1 implementation.
- Files saved to `FileStorage__LocalPath` (default `./uploads`).
- Storage path format: `{userId}/{guid}{extension}`. Never use the client-provided filename on disk.
- Accepted MIME types: `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`.
- Max file size: 5 MB. Validated in `ResumeService` before any I/O.
- `content_text` is always `null` in Sprint 1. Upload must succeed regardless.
- Soft delete: `DELETE` sets `IsActive = false`. List and get filter on `IsActive = true`.

**Sprint 2 — Azure Blob Storage:**
- `AzureBlobStorageService` will implement `IFileStorageService`.
- DI swap: `LocalFileStorageService` in Development, `AzureBlobStorageService` in non-Development.
- No service or controller code changes required.

---

## Frontend Rules

- TypeScript `strict: true`. No `any`. Ever.
- All API calls go through `lib/api-client.ts`. Never call `fetch()` directly in components.
- Auth state managed by `useAuth()` hook (TanStack Query, `queryKey: ['auth']`). JWT in `localStorage` (accepted MVP debt).
- Server state via TanStack Query `useQuery` and `useMutation`. Server Actions only for simple profile updates.
- Forms: React Hook Form + Zod. Schemas colocated with the form component.
- Every async operation must handle loading, error, and empty states explicitly.
- All API response types defined in `types/api.ts`.

---

## Current Sprint Scope

Sprint 1 is allowed to build:

- Auth module: register, login, get profile, update profile.
- Resumes module: upload, list, get, soft delete.
- Local file storage.
- Frontend: register page, login page, protected layout, resume upload page, resume list page.

Sprint 1 is **not** allowed to build:

- Text extraction (Sprint 2).
- Azure Blob Storage (Sprint 2).
- Any AI calls (Sprint 2).
- `JobDescriptions`, `Matching`, `JobTracking`, or `AI` modules.
- Mock Interview (post-MVP — do not scaffold at any point).
- CI/CD pipeline (Sprint 5).
- Dashboard summary (Sprint 4).

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
13. Mock Interview is post-MVP. Do not scaffold it.
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

- Controller-based API architecture adopted (replacing Minimal API route groups for business endpoints).
- Detailed documentation split into `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, and `docs/ROADMAP.md`.
- Sprint 1 uses local file storage; Azure Blob introduced in Sprint 2.
- `Resumes` module named plural to avoid C# namespace collision with the `Resume` entity class.
