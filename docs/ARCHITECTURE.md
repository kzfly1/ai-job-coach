# AI Job Coach — Architecture

This document describes the system architecture, the reasoning behind key decisions, and the conventions for extending the codebase. It is intended for both the solo developer and for coding agents working on this project.

For project scope, sprint plan, hard constraints, and configuration keys, see `docs/PROJECT_CONTEXT.md`.

---

## Overview

AI Job Coach is a full-stack web application with a clear client-server split. The frontend is a Next.js App Router application. The backend is a single ASP.NET Core Web API. There is no BFF, no API gateway, and no microservices. The two processes communicate over HTTPS/JSON.

```
┌─────────────────────────────────────────────────┐
│               Next.js Frontend                   │
│   App Router · Tailwind · shadcn/ui              │
│   TanStack Query · React Hook Form · Zod         │
└────────────────────┬────────────────────────────┘
                     │  HTTPS / JSON
┌────────────────────▼────────────────────────────┐
│          ASP.NET Core Web API (.NET 8)           │
│          Modular Monolith                        │
│                                                  │
│  ┌──────────────────────────────────────────┐   │
│  │  Auth · Resumes · JobDescriptions        │   │
│  │  Matching · JobTracking · AI             │   │
│  └──────────────────────────────────────────┘   │
│                                                  │
│  AppDbContext (EF Core 8)                        │
│  IFileStorageService                             │
│  IAIAnalysisService                              │
└──────────┬──────────────────┬───────────────────┘
           │                  │
  ┌────────▼───────┐  ┌───────▼──────────┐
  │  PostgreSQL     │  │  Azure Blob /     │
  │  (via Npgsql)   │  │  Local Storage   │
  └────────────────┘  └──────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │  OpenAI API          │
                    │  (direct HTTP, no   │
                    │  agent framework)   │
                    └─────────────────────┘
```

---

## Backend Architecture

### Why a Modular Monolith

The backend is a single deployable ASP.NET Core Web API project. All modules live inside it as namespace-separated folders. There are no separate assemblies or processes.

This is the right choice for a solo-developer MVP because it keeps EF Core migrations, DI registration, and build configuration simple. Module boundaries are enforced through folder structure, namespaces, and dependency rules — not by physical assembly separation. If a module needs to be extracted later, the folder structure already matches what a separate project would look like.

### Project Layout

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
│       ├── Resumes/
│       ├── JobDescriptions/        ← Sprint 2
│       ├── Matching/               ← Sprint 3
│       ├── JobTracking/            ← Sprint 4
│       └── AI/                    ← Sprint 2
│
└── AIJobCoach.SharedKernel/
    ├── Result.cs
    ├── PagedResult.cs
    └── Guard.cs
```

`AIJobCoach.SharedKernel` has zero dependencies on the Api project or any module. It contains only `Result<T>`, `Error`, `PagedResult<T>`, and `Guard`. It is never a dumping ground for HTTP concerns — those stay in `AIJobCoach.Api`.

---

## Module Structure

Every module follows the same four-layer folder structure. `Auth` and `Resumes` are the Sprint 1 examples.

```
Modules/Auth/
├── Domain/
│   └── User.cs
├── Application/
│   ├── AuthService.cs
│   ├── AuthDtos.cs
│   └── RegisterRequestValidator.cs
├── Infrastructure/
│   └── UserConfiguration.cs
└── Controllers/
    └── AuthController.cs

Modules/Resumes/
├── Domain/
│   └── Resume.cs                  ← entity class is singular; module folder is plural
├── Application/
│   ├── ResumeService.cs
│   ├── ResumeDtos.cs
│   └── IFileStorageService.cs
├── Infrastructure/
│   ├── ResumeConfiguration.cs
│   ├── LocalFileStorageService.cs
│   └── AzureBlobStorageService.cs ← Sprint 2
└── Controllers/
    └── ResumeController.cs
```

The module folder is named `Resumes` (plural) to avoid a namespace collision with the `Resume` entity class. The entity class itself remains `Resume`. This naming convention applies to any future module where the entity name matches the module name.

---

## Layer Responsibilities

### Domain

Contains entity classes only. No EF attributes, no DTOs, no HTTP types, no external dependencies.

```csharp
// Modules/Auth/Domain/User.cs
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FullName { get; private set; }
    public string? Headline { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Constructor or factory method — no public property setters for sensitive fields
}
```

All EF mapping lives in the corresponding `Infrastructure/` configuration class, not in the entity.

### Application

Contains service classes, DTOs, and FluentValidation validators. This is where business logic lives.

Application services receive and return domain types or DTOs. They do not know about HTTP, `HttpContext`, or `ClaimsPrincipal`. UserId is passed in as a plain `Guid` parameter, extracted upstream in the controller.

Application services inject `AppDbContext` directly. There is no repository interface between the service and EF Core — see the Data Access section for the rationale.

```csharp
// Modules/Resumes/Application/ResumeService.cs
public class ResumeService(AppDbContext db, IFileStorageService storage)
{
    public async Task<Result<ResumeDto>> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        Guid userId, CancellationToken ct = default)
    { ... }

    public async Task<ResumeDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    { ... }
}
```

### Infrastructure

Contains EF `IEntityTypeConfiguration<T>` classes and external service adapters (file storage, Azure Blob, OpenAI HTTP client).

```csharp
// Modules/Resumes/Infrastructure/ResumeConfiguration.cs
public class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("resumes");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FileName).IsRequired();
        // ...
    }
}
```

EF picks up all `IEntityTypeConfiguration` classes in the assembly via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`. No manual registration is needed when a new module adds a configuration class.

### Controllers

Controllers are thin HTTP adapters. They have three responsibilities: receive the HTTP request, extract identity from the JWT claims, and call an Application service. They do not contain business logic.

Controllers return `IActionResult`. They use the shared `ResultMapper` to translate `Result<T>` from the service layer into the appropriate HTTP status code.

---

## Controller API Style

### Attributes and Routing

```csharp
[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var validator = new RegisterRequestValidator();
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return UnprocessableEntity(validation.ToValidationErrorResponse());

        var result = await authService.RegisterAsync(request.Email, request.FullName, request.Password);
        // On success: set HttpOnly cookie, return { user: UserProfileDto }
        if (result.IsSuccess)
        {
            Response.Cookies.Append("access_token", result.Value!.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = !Environment.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(24),
                Path = "/"
            });
            return Created(string.Empty, new { user = result.Value.Profile });
        }
        return this.ToActionResult(result, _ => Ok());
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await authService.GetProfileAsync(userId);
        return this.ToActionResult(result, Ok);
    }
}
```

Key conventions:
- `[ApiController]` on every controller — enables automatic model binding and problem details.
- `[Route("api/{resource}")]` at the class level.
- `[Authorize]` on individual actions or the whole controller as appropriate.
- UserId extracted in the controller via `User.FindFirstValue(ClaimTypes.NameIdentifier)`. Services receive a plain `Guid`.
- Return `IActionResult`, not `IResult`.

### ResultMapper

`ResultMapper` is a static extension method on `ControllerBase` at `AIJobCoach.Api/Common/Http/ResultMapper.cs`. It centralises the translation from `Result<T>` to `IActionResult` so no controller contains a switch on error codes.

```csharp
public static class ResultMapper
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value!);

        return result.Error!.Code switch
        {
            "EMAIL_ALREADY_EXISTS"  => controller.Conflict(result.Error),
            "INVALID_CREDENTIALS"   => controller.StatusCode(401, result.Error),
            "USER_NOT_FOUND"        => controller.NotFound(result.Error),
            "RESUME_NOT_FOUND"      => controller.NotFound(result.Error),
            "UNSUPPORTED_FILE_TYPE" => controller.UnprocessableEntity(result.Error),
            "FILE_TOO_LARGE"        => controller.UnprocessableEntity(result.Error),
            _                       => controller.BadRequest(result.Error)
        };
    }
}
```

Add new error codes to this switch only when a new service introduces them. Do not add codes preemptively.

### Error Response Shapes

All error responses use one of three shapes:

```json
// Business error (known failure from Result<T>)
{ "code": "RESUME_NOT_FOUND", "message": "Resume not found." }

// Validation error (FluentValidation failure)
{ "code": "VALIDATION_ERROR", "message": "Validation failed.", "errors": { "email": ["Invalid email format."] } }

// Unexpected error (unhandled exception via GlobalExceptionHandler)
{ "code": "INTERNAL_ERROR", "message": "An unexpected error occurred.", "traceId": "..." }
```

### GlobalExceptionHandler

`GlobalExceptionHandler` implements `IExceptionHandler` and is registered via `app.UseExceptionHandler()`. It logs the full exception at `Error` level using Serilog and returns the `INTERNAL_ERROR` response. It never includes the exception message or stack trace in responses outside of the Development environment.

---

## Data Access Strategy

There is one `AppDbContext` at `AIJobCoach.Api/Data/AppDbContext.cs`. Application services inject it directly. There is no `IResumeRepository` or `IUserRepository`.

**Why no repository layer:** A repository interface wrapping EF Core with identical method signatures adds an abstraction with no practical benefit at this scale. EF Core's `DbContext` is already a unit of work and supports query composition, lazy loading prevention, change tracking, and transactions. Adding a wrapper makes the codebase harder to navigate and test without making it easier to swap the data store.

**When to extract a query class:** If multiple services share the same non-trivial query (complex joins, filtered aggregations), extract it to a focused query class inside the module's `Infrastructure/` layer. This is not the same as a general-purpose repository.

**AppDbContext setup:**

```csharp
// Data/AppDbContext.cs
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Resume> Resumes => Set<Resume>();
    // Add new DbSets here as modules are added

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`ApplyConfigurationsFromAssembly` automatically picks up every `IEntityTypeConfiguration<T>` class in the Api assembly, including those added by future modules. No manual registration is needed.

**Migrations** live in `AIJobCoach.Api/Migrations/`. Each sprint adds one migration covering only the tables introduced that sprint. Never create tables for future sprints in an earlier migration.

---

## File Storage Strategy

File storage is abstracted behind `IFileStorageService` in `Modules/Resumes/Application/`. This allows Sprint 1 to use local disk storage and Sprint 2 to swap to Azure Blob Storage without touching any service or controller code.

```csharp
public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream fileStream, string fileName,
        string contentType, Guid userId,
        CancellationToken ct = default);

    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
```

**Sprint 1 — LocalFileStorageService:**
Saves files to a directory configured via `FileStorage__LocalPath`. File names follow the pattern `{userId}/{guid}{extension}` — the original filename is never used on disk. The directory is created at startup if it does not exist.

**Sprint 2 — AzureBlobStorageService:**
Implements the same interface. Reads `AzureBlob__ConnectionString` and `AzureBlob__ContainerName` from configuration. Uses the same file naming convention. Registration is swapped in DI:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
else
    builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
```

No other code changes when the implementation is swapped.

**Soft delete:** `DELETE /api/resumes/{id}` sets `IsActive = false`. It does not call `IFileStorageService.DeleteAsync` — the physical file is retained. List and get queries filter on `IsActive = true`. This avoids the complexity of coordinating a DB delete with a Blob Storage delete in Sprint 1.

---

## AI Integration Strategy

The AI module lives at `Modules/AI/` and is introduced in Sprint 2. It contains the interface, the OpenAI implementation, and prompt templates as plain text files.

### Interface

```csharp
public interface IAIAnalysisService
{
    Task<JDAnalysisResult>     AnalyzeJobDescriptionAsync(string jdText, CancellationToken ct = default);
    Task<ResumeAnalysisResult> AnalyzeResumeAsync(string resumeText, CancellationToken ct = default);
    Task<MatchResult>          MatchResumeToJDAsync(string resumeText, string jdText, CancellationToken ct = default);
}
```

This interface has exactly three methods — one per AI capability in the MVP. Do not add interview-related methods.

### Prompt Management

Prompts are plain text files in `Modules/AI/Prompts/`. They are never hardcoded in C#. A `PromptLoader` service reads and caches them at startup and performs `{{placeholder}}` substitution.

```
Modules/AI/Prompts/
├── jd-analysis.txt
├── resume-analysis.txt
└── matching.txt
```

Every prompt includes a system instruction telling the model to return valid JSON only, with no markdown fences and no explanatory text. The expected JSON schema is embedded in the prompt.

### OpenAI HTTP Client

`OpenAIAnalysisService` uses a named `HttpClient` registered with a Polly retry policy (3 attempts, exponential backoff). It logs the raw response string before attempting to deserialise. On a parse failure it throws `AIResponseParseException` with the raw response attached.

```csharp
builder.Services.AddHttpClient("openai", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
})
.AddTransientHttpErrorPolicy(p =>
    p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

AI calls are synchronous from the HTTP request perspective — the controller waits for the result. This keeps the implementation simple. If response times prove problematic in production, a `BackgroundService` with a polling endpoint can be introduced without changing the interface.

### Cost Controls

- Default model: `gpt-4o-mini` (configurable via `OpenAI__Model`).
- Token usage logged at `Information` level on every response.
- Rate limiting on AI endpoints: 10 requests per user per hour, enforced in Sprint 6 via ASP.NET Core's built-in `RateLimiter`.

---

## Frontend Architecture

### Stack

- **Next.js** (latest stable) with App Router
- **TypeScript** — `strict: true`, no `any`
- **Tailwind CSS** (latest stable) for all styling
- **shadcn/ui** for UI primitives — do not hand-edit generated components
- **TanStack Query v5** for server state
- **React Hook Form + Zod** for form handling and validation

### Route Groups

```
app/
├── (auth)/            ← public: login, register
│   ├── login/
│   └── register/
└── (dashboard)/       ← protected: all authenticated pages
    ├── layout.tsx     ← redirect guard lives here
    ├── dashboard/
    ├── resumes/
    ├── jobs/
    ├── matches/
    └── applications/
```

The `(dashboard)/layout.tsx` handles authentication redirect. It reads `user` and `isLoading` from `useAuth()`. While `isLoading` is true it renders a skeleton to prevent flash of protected content. When `isLoading` is false and `user` is null it calls `router.replace('/login')`. This is client-side only — no Next.js middleware is used for auth in Sprint 1.

### API Client

All HTTP calls go through `lib/api-client.ts`. No component ever calls `fetch()` directly.

```typescript
// lib/api-client.ts
export async function apiClient<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL}${path}`, {
    ...options,
    credentials: 'include',       // sends the HttpOnly access_token cookie automatically
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const error: ApiError = await response.json();
    throw error;
  }

  return response.json() as Promise<T>;
}
```

`ApiError` is typed as `{ code: string; message: string; status: number }`. Components catch errors from `useMutation` and display them via toast.

`lib/auth.ts` (with `getToken`, `setToken`, `clearToken`) is not used — the JWT is never accessible to JavaScript. Auth state is managed entirely through `useAuth()` and the server cookie.

### Auth State

The JWT is stored in an `HttpOnly` cookie named `access_token`, set by the backend on login and register. JavaScript cannot read this cookie — the frontend never has access to the raw token.

`useAuth()` is backed by TanStack Query (`queryKey: ['auth', 'me']`). On mount it calls `GET /api/auth/me` to hydrate the current user. The hook returns `{ user: UserProfileDto | null, isLoading: boolean, logout: () => Promise<void> }`. Logout calls `POST /api/auth/logout`, which clears the cookie server-side, then removes the query from the cache and redirects to `/login`.

`lib/auth-api.ts` contains `login()`, `register()`, `getCurrentUser()`, and `logout()` — the four functions that call the auth endpoints. `useAuth()` in `lib/hooks/use-auth.ts` wraps `getCurrentUser()` with TanStack Query.

### Server State with TanStack Query

TanStack Query manages all async server state. The pattern for every data-fetching page is:

```typescript
// Fetch
const { data: resumes, isLoading, error } = useQuery({
  queryKey: ['resumes'],
  queryFn: () => apiClient<ResumeDto[]>('/api/resumes'),
  staleTime: 30_000,
});

// Mutate
const uploadMutation = useMutation({
  mutationFn: (file: File) => uploadResume(file),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['resumes'] });
    router.push('/resumes');
  },
});
```

Server Actions are used only for simple profile updates where no loading spinner or cache invalidation is needed. All AI-triggered operations and file uploads use `useMutation`.

### Component Conventions

- **Page components** (`app/**page.tsx`) are thin — they read URL params, call hooks, pass data down.
- **Feature components** (`components/`) own display and interaction logic.
- **Custom hooks** (`lib/hooks/`) encapsulate TanStack Query calls and mutation logic. No business logic lives in components directly.
- Every async operation handles three states explicitly: loading (skeleton, not spinner for lists), error (toast or inline message), and empty (helpful prompt with a call to action).

### Types

All API response types are defined in `types/api.ts` and mirror the backend DTOs. They are the single source of truth for the shape of data flowing across the network. No inline type assertions against API responses.

---

## Testing Strategy

### Unit Tests (xUnit + Moq + FluentAssertions)

Target the Application layer. Test service methods in isolation with mocked dependencies.

What to unit test:
- Service business logic (validation, ownership checks, state transitions)
- `ResultMapper` — verify each error code maps to the correct HTTP status
- `PromptLoader` — verify placeholder substitution and error on missing key
- `RegisterRequestValidator` — valid and invalid inputs
- `ResumeTextExtractor` (Sprint 2) — valid PDF, valid DOCX, corrupt file returns null

What not to unit test with mocks:
- Controllers — covered by integration tests
- EF configurations — covered by migration inspection
- `LocalFileStorageService` — covered by file system unit tests

### Service / Storage Tests

`LocalFileStorageService` and `AzureBlobStorageService` should be tested with real file system or real Blob Storage respectively. Mark Azure-dependent tests with `[Category("AzureIntegration")]` and exclude them from the standard CI run.

### Integration Tests (WebApplicationFactory)

Each module's endpoints are covered by integration tests using `WebApplicationFactory<Program>` with a real test database. These tests exercise the full stack from HTTP request to database and back.

Cover per endpoint:
- Happy path (correct status code and response shape)
- Auth failure (missing or invalid cookie → 401)
- Validation failure (invalid input → 422)
- Not found (wrong id or wrong user → 404)

Mark tests that call real external services (OpenAI, Azure Blob) with `[Category("ManualOnly")]` — they are run manually, not in CI.

### CI Test Exclusions

```csharp
dotnet test --filter "Category!=ManualOnly&Category!=AIIntegration&Category!=AzureIntegration"
```

---

## Extension Guidelines

### Adding a New Module

Follow these steps in order. Do not create the module folder until the sprint begins.

1. **Create the folder structure** with only the files needed for the current task:
   ```
   Modules/{ModuleName}/
   ├── Domain/
   │   └── {Entity}.cs
   ├── Application/
   │   ├── {Module}Service.cs
   │   ├── {Module}Dtos.cs
   │   └── {Module}Validator.cs     ← only if the module has validated input
   ├── Infrastructure/
   │   └── {Entity}Configuration.cs
   └── Controllers/
       └── {Module}Controller.cs
   ```

2. **Add the entity** to `AppDbContext` as a new `DbSet<{Entity}>`.

3. **Add an EF migration** covering only the new tables:
   ```bash
   dotnet ef migrations add Add{ModuleName}
   ```

4. **Register services** in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<{Module}Service>();
   ```
   No module registration method is needed — controllers are picked up automatically by `MapControllers()`, and EF configurations are picked up by `ApplyConfigurationsFromAssembly`.

5. **Write the service** in Application, injecting `AppDbContext` directly.

6. **Write the controller** in Controllers, calling the service and mapping results via `ResultMapper`.

7. **Add frontend** — create the page route, a TanStack Query hook, and any shared components needed.

8. **Write tests** — unit tests for the service, integration tests for the controller endpoints.

### Adding a New API Error Code

1. Add the error code string as a constant or inline string in the service that returns it.
2. Add a new case to the `switch` in `ResultMapper.ToActionResult`.
3. Document the code in this file under the module's section.

### Adding a New AI Capability

1. Add a method to `IAIAnalysisService`.
2. Implement the method in `OpenAIAnalysisService`.
3. Create the prompt file in `Modules/AI/Prompts/`.
4. Register the prompt in `PromptLoader` if it uses static loading.

Do not add agent orchestration, tool calling, or multi-step pipelines. A single structured prompt per operation is the pattern for this MVP.

### Adding a New Prompt

Prompts follow this convention:

```
You are an expert technical recruiter.

Given the following job description:
{{job_description}}

Extract and return only a JSON object with this schema:
{
  "technicalSkills": string[],
  "softSkills": string[],
  "experienceLevel": "junior" | "mid" | "senior",
  "roleType": string
}

Respond only with valid JSON. Do not include markdown, explanation, or any text outside the JSON object.
```

The system prompt instruction to return JSON only must appear in every prompt. The schema must be embedded in the prompt — do not rely on the model to infer it.

---

*Last updated: Sprint 1 — auth updated from localStorage JWT to HttpOnly cookie; apiClient updated to use credentials: include; useAuth updated to hydrate from GET /api/auth/me.*
*Update this document when a new module is added, a layer responsibility changes, or an architectural decision is revised.*
