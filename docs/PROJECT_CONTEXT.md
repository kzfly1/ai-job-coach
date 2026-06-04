# AI Job Coach — Project Context

> **For coding agents (Claude Code, Codex, Cursor, etc.)**
> This is the authoritative reference for architecture decisions, conventions, and constraints.
> Read this entire document before generating any code.
> Do not deviate from these patterns without explicit instruction.
> When in doubt: simpler is correct. Do less, not more.

---

## Project Status

**Current state:** Empty repository. No code exists yet.
**Phase:** MVP — Sprint 1 in progress.
**Goal:** Ship a working MVP in 3 months as a solo developer. Validate product value. Demonstrate full-stack + AI engineering.

---

## Product Summary

**AI Job Coach** helps early-career developers (students, interns, junior engineers) close the gap between their current profile and job market expectations.

Core user flows:
1. Upload resume → get AI analysis of skills and profile
2. Paste a Job Description → get AI extraction of requirements
3. Match resume against JD → get match score, skill gaps, actionable suggestions
4. Start a mock interview → get AI-generated questions + feedback on answers
5. Track job applications → status pipeline with timeline

---

## Critical Instruction: Build Vertical Slices Only

**Do not scaffold all modules at once. Do not create empty folders as placeholders.**

Build one complete vertical slice at a time: backend endpoint → service → DB migration → frontend page. Only create files that are required by the current task. An empty `Modules/Interview/` folder created in Sprint 1 is dead weight and noise.

The module list below is the full target picture. In Sprint 1, only `Auth` and `Resume` modules exist. Everything else is future work.

---

## Target Repository Structure

This is the end-state layout. **Only create what the current sprint requires.**

```
ai-job-coach/
├── src/
│   ├── AIJobCoach.Api/                  # ASP.NET Core Web API — single entry point
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Middleware/
│   │       └── GlobalExceptionHandler.cs
│   │
│   ├── AIJobCoach.SharedKernel/         # Zero dependencies on other modules
│   │   ├── Result.cs                    # Result<T> + Error record
│   │   ├── PagedResult.cs
│   │   └── Guard.cs
│   │
│   └── Modules/
│       ├── Auth/                        # Sprint 1
│       │   ├── Domain/
│       │   │   └── User.cs
│       │   ├── Application/
│       │   │   ├── AuthService.cs
│       │   │   ├── AuthDtos.cs
│       │   │   └── RegisterRequestValidator.cs
│       │   ├── Infrastructure/
│       │   │   └── UserConfiguration.cs  # EF IEntityTypeConfiguration
│       │   └── Endpoints/
│       │       └── AuthEndpoints.cs
│       │
│       ├── Resume/                      # Sprint 1
│       │   ├── Domain/
│       │   │   └── Resume.cs
│       │   ├── Application/
│       │   │   ├── ResumeService.cs
│       │   │   ├── ResumeDtos.cs
│       │   │   └── IFileStorageService.cs
│       │   ├── Infrastructure/
│       │   │   ├── ResumeConfiguration.cs
│       │   │   ├── LocalFileStorageService.cs  # Sprint 1
│       │   │   └── AzureBlobStorageService.cs  # Sprint 2, before staging
│       │   └── Endpoints/
│       │       └── ResumeEndpoints.cs
│       │
│       ├── JobDescription/              # Sprint 2
│       ├── Matching/                    # Sprint 3
│       ├── Interview/                   # Sprint 4
│       ├── JobTracking/                 # Sprint 5
│       │
│       └── AI/                         # Sprint 2
│           ├── IAIAnalysisService.cs
│           ├── OpenAIAnalysisService.cs
│           └── Prompts/
│               ├── jd-analysis.txt
│               ├── resume-analysis.txt
│               ├── matching.txt
│               └── interview-generation.txt
│
├── frontend/                            # Next.js App Router
│   ├── app/
│   │   ├── (auth)/
│   │   │   ├── login/page.tsx
│   │   │   └── register/page.tsx
│   │   ├── (dashboard)/
│   │   │   ├── layout.tsx              # Protected — redirects unauthenticated users
│   │   │   ├── dashboard/page.tsx
│   │   │   ├── resumes/page.tsx
│   │   │   ├── jobs/page.tsx           # Sprint 2+
│   │   │   ├── matches/page.tsx        # Sprint 3+
│   │   │   └── interviews/page.tsx     # Sprint 4+
│   │   └── layout.tsx
│   ├── components/
│   │   ├── ui/                         # shadcn/ui — do not hand-edit
│   │   └── shared/                     # App-specific shared components
│   ├── lib/
│   │   ├── api-client.ts               # Typed fetch wrapper — all API calls go here
│   │   ├── auth.ts                     # Token storage + auth helpers
│   │   └── hooks/                      # TanStack Query hooks
│   └── types/
│       └── api.ts                      # TypeScript types mirroring backend DTOs
│
├── docs/
│   └── PROJECT_CONTEXT.md              # This file
├── docker-compose.yml
└── .github/
    └── workflows/
        └── deploy.yml                  # Added in Sprint 6
```

---

## Sprint 1 — Implementation Scope

**Only build what is listed here. Nothing else.**

### Backend
- `docker-compose.yml` with PostgreSQL 16
- `AIJobCoach.Api` project: `Program.cs`, `appsettings.json`, `GlobalExceptionHandler`, Serilog
- `AIJobCoach.SharedKernel`: `Result<T>`, `Error`, `Guard`
- `AppDbContext` with `users` and `resumes` tables
- EF migration: `InitialCreate` (users + resumes only)
- `Auth` module: `User` entity, `AuthService`, register + login + me endpoints
- `Resume` module: `Resume` entity, `ResumeService`, upload + list + get endpoints
- `IFileStorageService` interface + `LocalFileStorageService` implementation (saves to `wwwroot/uploads/` or a configured local path)
- PDF text extraction with `PdfPig`; DOCX extraction with `DocumentFormat.OpenXml`

### Frontend
- Next.js project scaffold with TypeScript strict mode, Tailwind, shadcn/ui, TanStack Query
- `apiClient` wrapper in `lib/api-client.ts`
- Register page, login page (React Hook Form + Zod)
- Protected `(dashboard)` layout with auth redirect
- Resume upload page (drag-and-drop, file validation, upload progress)
- Resume list page (table of uploaded resumes)

### What is explicitly NOT in Sprint 1
- Azure Blob Storage (added in Sprint 2 before staging deploy)
- Any AI calls
- JD, Matching, Interview, or Job Tracking modules
- Dashboard summary stats
- CI/CD pipeline

---

## Backend — Architecture and Conventions

### Stack
- **Runtime:** .NET 8
- **Framework:** ASP.NET Core Web API with Minimal APIs
- **ORM:** EF Core 8 with PostgreSQL (Npgsql provider)
- **Validation:** FluentValidation
- **Logging:** Serilog, console sink, structured JSON output
- **Testing:** xUnit + Moq + FluentAssertions
- **HTTP resilience:** Polly — applied to AI HTTP client only

### Architecture: Modular Monolith

Each module is a logical namespace boundary inside a single deployable process. Modules are not separate assemblies and not separate processes. Future extraction to microservices is possible but is not a current goal — do not design for it.

**Module internal layers:**
```
Modules/{ModuleName}/
  Domain/          # Entities only. No EF attributes, no HTTP types, no external dependencies.
  Application/     # Service classes, DTOs, FluentValidation validators.
                   # Depends on: Domain, SharedKernel, AppDbContext (see note below).
  Infrastructure/  # EF IEntityTypeConfiguration, external service adapters.
                   # Depends on: Application, Domain.
  Endpoints/       # Minimal API route group. Thin — parse request, call service, return IResult.
                   # Depends on: Application only.
```

### Dependency rule and the DbContext trade-off

In a strict Clean Architecture, Application services depend only on Domain and repository interfaces, with Infrastructure providing concrete implementations. **For this MVP, that boundary is intentionally relaxed.**

Application services inject `AppDbContext` directly. There is no `IResumeRepository`. This is a conscious trade-off:

- **Why:** A repository interface wrapping EF Core with identical method signatures adds a layer of indirection with no practical benefit at this scale and team size. It makes the codebase harder to navigate, not easier.
- **Consequence accepted:** Application layer has a compile-time dependency on EF Core. This means extraction to a different data store later requires changing Application code, not just swapping an Infrastructure implementation.
- **When to revisit:** If a module's data access logic becomes complex enough that multiple services share non-trivial query logic, extract that into a focused query class inside Infrastructure. Not before.

Do not introduce `IRepository<T>`, `IResumeRepository`, or any generic repository abstraction. If you find yourself about to create one, stop and ask.

### Single AppDbContext

One `AppDbContext` registered in `AIJobCoach.Api`. Each module registers its own entity type configuration via `IEntityTypeConfiguration<T>` in its `Infrastructure/` layer, which is then discovered by EF Core's assembly scanning.

```csharp
// AIJobCoach.Api/Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    // If modules are separate assemblies, scan each:
    // modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserConfiguration).Assembly);
}
```

Do not create per-module DbContexts.

### API style: Minimal API route groups

Use `RouteGroupBuilder` extension methods per module, not controllers.

```csharp
// Modules/Resume/Endpoints/ResumeEndpoints.cs
public static class ResumeEndpoints
{
    public static RouteGroupBuilder MapResumeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", UploadResume).RequireAuthorization();
        group.MapGet("/", GetResumes).RequireAuthorization();
        group.MapGet("/{id:guid}", GetResume).RequireAuthorization();
        return group;
    }

    private static async Task<IResult> UploadResume(
        IFormFile file,
        ResumeService resumeService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // parse → call service → return typed IResult
    }
}

// Program.cs
app.MapGroup("/api/resumes").MapResumeEndpoints();
app.MapGroup("/api/auth").MapAuthEndpoints();
```

### Error handling

There are two categories of failure. Handle them differently.

**Expected failures** — business rule violations, not-found, validation errors, conflict. Return typed `IResult` directly from the endpoint. Do not throw exceptions for these.

```csharp
// Examples of expected failure responses
Results.NotFound(new { code = "RESUME_NOT_FOUND", message = $"Resume '{id}' does not exist." })
Results.Conflict(new { code = "EMAIL_ALREADY_EXISTS", message = "An account with this email already exists." })
Results.UnprocessableEntity(new { code = "TEXT_EXTRACTION_FAILED", message = "Could not extract text from this file." })
Results.ValidationProblem(errors)   // FluentValidation failures via filter
```

**Unexpected failures** — unhandled exceptions, infrastructure failures, bugs. Caught by `GlobalExceptionHandler` middleware, logged at `Error` level with full stack trace, and returned as:

```json
{
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred.",
  "traceId": "00-abc123..."
}
```

The `GlobalExceptionHandler` must never leak exception messages or stack traces to the client in Production.

### Authentication

Custom implementation — do not use ASP.NET Identity.

- **Password hashing:** BCrypt via `BCrypt.Net-Next`. Work factor 12. Never store plaintext passwords. Never return `password_hash` in any response.
- **JWT:** `System.IdentityModel.Tokens.Jwt`. Token expiry 24 hours. Claims: `sub` (userId as string), `email`.
- **JWT secret:** Minimum 32 characters. Validated at startup — throw `InvalidOperationException` on startup if secret is missing or under 32 chars.
- **Duplicate email on register:** Return `409 Conflict` with code `EMAIL_ALREADY_EXISTS`.
- **Invalid credentials on login:** Return `401 Unauthorized` with a **generic** message: `"Invalid email or password."` — never indicate which field was wrong.
- **No refresh tokens** in MVP. 24-hour expiry, re-login on expiry.
- Endpoints opt in via `.RequireAuthorization()`. Public endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `GET /health`.

### File storage: local first, Azure later

Sprint 1 uses local file storage. Azure Blob Storage is integrated in Sprint 2 before the first staging deployment.

```csharp
// Modules/Resume/Application/IFileStorageService.cs
public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string fileIdentifier, CancellationToken ct = default);
}

// Sprint 1: LocalFileStorageService — saves to a configured local directory
// Sprint 2: AzureBlobStorageService — replaces local via DI registration
```

Register via DI based on environment:
```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
else
    builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
```

File naming: `{userId}/{guid}{extension}`. Never use the client-provided filename on disk.

Accepted MIME types: `application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`. Max size: 5 MB. Validate before saving.

### Configuration

All secrets via environment variables or `appsettings.{Environment}.json`. Never commit secrets. Add `appsettings.Development.json` to `.gitignore`.

```
# Sprint 1
ConnectionStrings__DefaultConnection
Jwt__Secret                          # Min 32 chars — validated at startup
Jwt__Issuer
Jwt__Audience
FileStorage__LocalPath               # e.g. ./uploads — Sprint 1 only

# Sprint 2+
AzureBlob__ConnectionString
AzureBlob__ContainerName
OpenAI__ApiKey
OpenAI__Model                        # Default: gpt-4o-mini
```

---

## AI Module — Conventions (Sprint 2+)

Do not create the AI module in Sprint 1.

### Interface

```csharp
public interface IAIAnalysisService
{
    Task<JDAnalysisResult> AnalyzeJobDescriptionAsync(string jdText, CancellationToken ct = default);
    Task<ResumeAnalysisResult> AnalyzeResumeAsync(string resumeText, CancellationToken ct = default);
    Task<MatchResult> MatchResumeToJDAsync(string resumeText, string jdText, CancellationToken ct = default);
    Task<InterviewQuestionsResult> GenerateInterviewQuestionsAsync(string resumeText, string jdText, CancellationToken ct = default);
    Task<InterviewFeedbackResult> EvaluateAnswerAsync(string question, string answer, string context, CancellationToken ct = default);
}
```

### Prompt management

Prompts live in `Modules/AI/Prompts/*.txt`. Never hardcode prompt text in C# strings. A `PromptLoader` service reads and caches prompts from disk at startup. Prompts use `{{placeholder}}` syntax for variable substitution.

### Structured output contract

Every AI call instructs the model to return JSON only. Include in every system prompt: *"Respond only with valid JSON matching the schema below. Do not include markdown code fences, explanations, or any text outside the JSON object."*

Always log the raw response string before attempting to deserialize. On parse failure, throw `AIResponseParseException` with the raw response string attached as a property. Never silently swallow a parse failure.

### Retry policy

```csharp
builder.Services.AddHttpClient("openai")
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

Applied to the OpenAI named `HttpClient` only — not to the general API.

### Cost control

- Default model: `gpt-4o-mini` (configurable via `OpenAI__Model`)
- Log token usage from every response at `Information` level
- Rate limiting on AI endpoints (10 calls/user/hour) added in Sprint 6

---

## Database Schema — Incremental by Sprint

**Do not create tables that a sprint does not need.** Run `dotnet ef migrations add` at the start of each sprint for that sprint's tables only.

### Sprint 1 migration: `InitialCreate`

```sql
users (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email         TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  full_name     TEXT NOT NULL,
  headline      TEXT,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
)

resumes (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  file_name     TEXT NOT NULL,         -- original filename for display
  storage_path  TEXT NOT NULL,         -- local path (Sprint 1) or blob URL (Sprint 2+)
  content_text  TEXT,                  -- null if extraction failed; not a hard error
  is_active     BOOLEAN NOT NULL DEFAULT true,
  uploaded_at   TIMESTAMPTZ NOT NULL DEFAULT now()
)

CREATE INDEX idx_resumes_user_id ON resumes(user_id);
```

### Sprint 2 migration: `AddJobDescriptionsAndResumeAnalyses`

```sql
resume_analyses (
  id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  resume_id        UUID NOT NULL REFERENCES resumes(id) ON DELETE CASCADE,
  summary          TEXT,
  skills_extracted JSONB,    -- { technical: [], soft: [], projects: [] }
  raw_response     TEXT,     -- always store for debugging
  created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
)

job_descriptions (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  title        TEXT NOT NULL,
  company      TEXT,
  raw_text     TEXT NOT NULL,
  requirements JSONB,        -- { technical: [], soft: [], experience_level: "", role_type: "" }
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
)

CREATE INDEX idx_resume_analyses_resume_id ON resume_analyses(resume_id);
CREATE INDEX idx_job_descriptions_user_id ON job_descriptions(user_id);
```

### Sprint 3 migration: `AddMatchResults`

```sql
match_results (
  id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id            UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  resume_id          UUID NOT NULL REFERENCES resumes(id),
  job_description_id UUID NOT NULL REFERENCES job_descriptions(id),
  match_score        NUMERIC(5,2),    -- 0.00–100.00
  skill_gaps         JSONB,           -- [{ skill, importance, suggestion }]
  suggestions        JSONB,           -- [{ title, detail, priority }]
  raw_response       TEXT,
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
)

CREATE INDEX idx_match_results_user_id ON match_results(user_id);
```

### Sprint 4 migration: `AddInterviews`

```sql
interview_sessions (
  id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id            UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  resume_id          UUID REFERENCES resumes(id),
  job_description_id UUID REFERENCES job_descriptions(id),
  status             TEXT NOT NULL DEFAULT 'generated',  -- generated|in_progress|completed
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
)

interview_questions (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  session_id    UUID NOT NULL REFERENCES interview_sessions(id) ON DELETE CASCADE,
  question_text TEXT NOT NULL,
  question_type TEXT NOT NULL,   -- behavioral|technical
  order_index   INT NOT NULL,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
)

interview_answers (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  question_id  UUID NOT NULL REFERENCES interview_questions(id) ON DELETE CASCADE,
  answer_text  TEXT NOT NULL,
  feedback     JSONB,            -- { score: 1-5, strengths: [], improvements: [], sample_answer: "" }
  raw_response TEXT,
  submitted_at TIMESTAMPTZ NOT NULL DEFAULT now()
)
```

### Sprint 5 migration: `AddJobTracking`

```sql
job_applications (
  id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id              UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  company_name         TEXT NOT NULL,
  job_title            TEXT NOT NULL,
  job_description_text TEXT,
  job_description_id   UUID REFERENCES job_descriptions(id),   -- nullable
  status               TEXT NOT NULL DEFAULT 'saved',          -- saved|applied|interviewing|offer|rejected
  applied_at           TIMESTAMPTZ,
  notes                TEXT,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at           TIMESTAMPTZ NOT NULL DEFAULT now()
)

application_timeline_events (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  application_id UUID NOT NULL REFERENCES job_applications(id) ON DELETE CASCADE,
  event_type     TEXT NOT NULL,   -- status_change|note|interview_scheduled|offer_received
  description    TEXT,
  occurred_at    TIMESTAMPTZ NOT NULL DEFAULT now()
)

CREATE INDEX idx_job_applications_user_id ON job_applications(user_id);
CREATE INDEX idx_job_applications_status ON job_applications(status);
```

---

## API Endpoints Reference

Build endpoints sprint by sprint. Do not stub future endpoints.

```
# Sprint 1
POST   /api/auth/register
POST   /api/auth/login
GET    /api/auth/me
PUT    /api/auth/me
GET    /health

POST   /api/resumes                  multipart/form-data
GET    /api/resumes
GET    /api/resumes/{id}
DELETE /api/resumes/{id}

# Sprint 2
POST   /api/resumes/{id}/analyze
POST   /api/job-descriptions
GET    /api/job-descriptions
GET    /api/job-descriptions/{id}
DELETE /api/job-descriptions/{id}
POST   /api/job-descriptions/{id}/analyze

# Sprint 3
POST   /api/matches                  { resumeId, jobDescriptionId }
GET    /api/matches/{id}

# Sprint 4
POST   /api/interviews               { resumeId, jobDescriptionId }
GET    /api/interviews/{id}
GET    /api/interviews/{id}/questions
POST   /api/interviews/{id}/questions/{questionId}/answer

# Sprint 5
GET    /api/applications
POST   /api/applications
GET    /api/applications/{id}
PUT    /api/applications/{id}
DELETE /api/applications/{id}
POST   /api/applications/{id}/timeline
GET    /api/dashboard/summary
```

---

## Frontend — Conventions and Constraints

### Stack
- **Framework:** Next.js 14, App Router
- **Language:** TypeScript, `"strict": true` in `tsconfig.json`
- **Styling:** Tailwind CSS + shadcn/ui
- **Forms:** React Hook Form + Zod
- **Server state:** TanStack Query (React Query v5)
- **HTTP:** Typed `apiClient` wrapper — never call `fetch()` directly in components

### When to use Server Actions vs TanStack Query

| Use Server Actions for | Use TanStack Query for |
|---|---|
| Auth form submissions (login, register) | All AI-triggered operations |
| Simple profile updates | Any operation needing a loading spinner |
| | Data with refetch or cache invalidation requirements |

Rule of thumb: if the operation might take >1 second or the user needs to see a progress state, use TanStack Query.

### Auth state

- JWT stored in `localStorage` — accepted technical debt, documented below
- Auth state managed via a `useAuth()` hook backed by TanStack Query
- `(dashboard)/layout.tsx` redirects unauthenticated users to `/login`

### Component conventions

- Page components in `app/` are thin — fetch data, pass to feature components
- Feature components in `components/` own display logic
- No business logic in components — extract to custom hooks
- Every async operation must handle three states: loading, error, empty

### TypeScript

- `"strict": true`. No `any`. Use `unknown` and narrow with type guards.
- All API response shapes defined in `types/api.ts`
- Zod schemas colocated with the form component that uses them

---

## Local Development Setup

### Prerequisites
- .NET 8 SDK
- Node.js 20+
- Docker Desktop

### Start local environment
```bash
docker-compose up -d                # PostgreSQL on port 5432
cd src/AIJobCoach.Api
dotnet run                          # API on https://localhost:7001
cd frontend
npm install && npm run dev          # Frontend on http://localhost:3000
```

### docker-compose.yml

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: aijobcoach
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

### EF migrations

```bash
cd src/AIJobCoach.Api
dotnet ef migrations add {MigrationName} --project ../AIJobCoach.Api
dotnet ef database update
```

---

## Git Conventions

### Branch naming
```
feat/{short-description}
fix/{short-description}
chore/{short-description}
```

### Commit format (Conventional Commits)
```
feat(auth): implement JWT login endpoint
feat(resume): add local file storage service
fix(auth): return generic message on invalid credentials
chore(deps): upgrade EF Core to 8.0.6
```

### Branch strategy
```
main      ← production; no direct pushes
develop   ← integration target for all feature branches
feat/*    ← cut from develop, PR back to develop
```

---

## Deployment (Target — Sprint 6)

| Environment | Infrastructure |
|---|---|
| Local | Docker Compose |
| Staging | Azure App Service (B1) + Azure DB for PostgreSQL (Burstable) |
| Production | Azure App Service (B2) + Azure DB for PostgreSQL (General Purpose) |

Azure Blob Storage uses separate containers per environment: `resumes-dev`, `resumes-staging`, `resumes-prod`.

Secrets stored in Azure App Service environment variables — not Key Vault (over-engineering for a solo MVP).

CI/CD via GitHub Actions — set up in Sprint 6.

---

## Hard Constraints

These are non-negotiable. If a task appears to require one of these patterns, stop and ask before proceeding.

1. **No microservices.** One process. One deployment unit.
2. **No message queues** (RabbitMQ, Azure Service Bus, Kafka). Add only if a measured blocking problem exists.
3. **No vector database.** Add `pgvector` only if semantic search over a corpus is a proven requirement.
4. **No agent frameworks** (Semantic Kernel, LangChain, AutoGen). Direct `HttpClient` calls to OpenAI API only.
5. **No AutoMapper.** Manual mapping via static `ToDto()` methods or constructors.
6. **No MediatR / CQRS.** Direct service injection and calls.
7. **No repository pattern** wrapping EF Core. Inject `AppDbContext` directly into Application services.
8. **No per-module DbContexts.** One `AppDbContext`.
9. **No refresh tokens** in MVP. 24-hour JWT expiry, re-login on expiry.
10. **No `any` in TypeScript.** Ever.
11. **No empty placeholder folders.** Only create files and directories that a current task requires.

---

## Sprint Progress

| Sprint | Weeks | Focus | Status |
|---|---|---|---|
| 1 | 1–2 | Scaffold + Auth + Resume Upload (local storage) | 🔲 Not started |
| 2 | 3–4 | AI Service + JD Analysis + Resume Analysis + Azure Blob | 🔲 Not started |
| 3 | 5–6 | Matching — full vertical slice | 🔲 Not started |
| 4 | 7–8 | Mock Interview — full vertical slice | 🔲 Not started |
| 5 | 9–10 | Job Tracking + Dashboard summary | 🔲 Not started |
| 6 | 11–12 | Production deploy + rate limiting + observability | 🔲 Not started |

---

## Known Technical Debt (Accepted for MVP)

| Item | Risk | When to address |
|---|---|---|
| JWT in `localStorage` (not httpOnly cookie) | XSS vulnerability | Post-MVP, before public launch |
| Application services depend on `AppDbContext` directly | Harder to swap data store; no interface to mock | Introduce query objects if a module's data access grows complex |
| Local file storage in Sprint 1 | Files lost on container restart | Replaced with Azure Blob before first staging deploy (Sprint 2) |
| Synchronous AI calls | Long response times (5–15s) | Add `BackgroundService` + polling if user feedback confirms it's painful |
| No email verification on register | Fake accounts | Post-MVP |
| AI prompt versions not tracked in DB | Can't reproduce historical analyses | Post-MVP |
| No request idempotency keys on AI endpoints | Duplicate AI calls on network retry | Post-MVP |

---

*Last updated: Sprint 0 — project start*
*Maintained by: solo developer*
*Do not auto-update this file. Update manually when architectural decisions change.*
