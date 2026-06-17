# AI Job Coach — Sprint Roadmap

This document describes the planned work for Sprints 2–6. It is intentionally higher-level than the sprint ticket files — it gives enough context to understand the goal and acceptance bar for each sprint without duplicating the detail in individual tickets.

For architectural decisions, see `docs/DECISIONS.md`. For implementation conventions, see `docs/PROJECT_CONTEXT.md` and `docs/ARCHITECTURE.md`.

---

## Current Foundation (Sprint 1)

Sprint 1 establishes the project skeleton and the first two complete vertical slices.

**Backend:** ASP.NET Core Web API with one `AppDbContext`, SharedKernel (`Result<T>`, `Error`, `Guard`), Serilog structured logging, GlobalExceptionHandler, and ResultMapper. Auth module with custom JWT + BCrypt. Resumes module with `IFileStorageService`, `LocalFileStorageService`, and full CRUD including soft delete. EF InitialCreate migration with `users` and `resumes` tables.

**Frontend:** Next.js App Router, TypeScript strict mode, TanStack Query v5, shadcn/ui. `apiClient` wrapper with `credentials: 'include'`, `useAuth()` hook backed by `GET /api/auth/me`, register and login pages (React Hook Form + Zod), HttpOnly cookie auth flow, protected dashboard layout, resume upload page with XHR progress, and resume list page.

**What is not yet done:** text extraction, AI calls, job description analysis, matching, application tracking, Azure storage, and CI/CD.

---

## Sprint 2 — Text Extraction, AI Analysis, and Azure Blob

**Weeks 3–4**

### Goal

Introduce the AI pipeline and replace local file storage with Azure Blob. By the end of this sprint a user can upload a resume, extract its text, analyse it with AI, and do the same for a job description. Azure Blob Storage is in place before any staging deployment.

### Product Deliverables

- Resume text is extracted on upload (PDF and DOCX).
- User can trigger AI analysis of a resume and view extracted skills, experience level, and a summary.
- User can paste a job description, save it, and view AI-extracted requirements and skill keywords.
- Resume list page shows whether text has been extracted.

### Backend Deliverables

- `AzureBlobStorageService` implementing `IFileStorageService`. Registered in non-Development via DI; `LocalFileStorageService` remains in Development.
- `ResumeTextExtractor` using `PdfPig` (PDF) and `DocumentFormat.OpenXml` (DOCX). Extraction failure sets `content_text = null` and logs a warning — it does not fail the upload.
- Sprint 2 EF migration: `resume_analyses` and `job_descriptions` tables.
- AI module: `IAIAnalysisService` with three methods (`AnalyzeResumeAsync`, `AnalyzeJobDescriptionAsync`, `MatchResumeToJDAsync`). `OpenAIAnalysisService` using a named `HttpClient` with Polly retry (3 attempts, exponential backoff). `PromptLoader` reading from `Modules/AI/Prompts/`.
- `POST /api/resumes/{id}/analyze` — requires `content_text` to be non-null, returns `422` otherwise.
- `POST /api/job-descriptions` — creates and immediately analyses the JD.
- `GET /api/job-descriptions`, `GET /api/job-descriptions/{id}`, `DELETE /api/job-descriptions/{id}`.
- `GET /api/resumes/{id}/analysis` — returns stored analysis result.

### Frontend Deliverables

- Resume detail page: shows extraction status, "Analyse Resume" button, and analysis results (skills as badges, summary paragraph).
- `/jobs/new` page: form to paste a JD with title and optional company. Submits to `POST /api/job-descriptions`.
- JD results view: displays technical skills, soft skills, experience level, and role type after creation.
- `/jobs` list page: table of saved JDs with title, company, and date.

### Testing Deliverables

- Unit tests for `ResumeTextExtractor`: valid PDF, valid DOCX, corrupt file returns `null`.
- Unit tests for `OpenAIAnalysisService`: response parsing, `AIResponseParseException` on invalid JSON.
- Unit tests for `PromptLoader`: placeholder substitution, error on missing key.
- Integration tests for `POST /api/resumes/{id}/analyze`: success, `422` when `content_text` is null, `503` when OpenAI is unreachable.
- Integration tests for `POST /api/job-descriptions`: success with analysis populated.
- Azure integration tests marked `[Category("AzureIntegration")]` — run manually, not in CI.

### Acceptance Criteria

- Upload a real PDF resume → `content_text` is populated.
- Click "Analyse Resume" → skills and summary displayed within 15 seconds.
- Paste a real job description → requirements and skill keywords displayed.
- All Sprint 1 resume upload and list flows still work against Azure Blob in the Development environment swap test.
- `POST /api/resumes/{id}/analyze` returns `422` for a resume where text extraction failed.
- No AI call is made in Development without a valid `OpenAI__ApiKey` configured.

---

## Sprint 3 — Resume-to-Job Matching

**Weeks 5–6**

### Goal

Deliver the primary value proposition of the product. A user can select an analysed resume and an analysed job description, run a match, and receive a match score with skill gaps and improvement suggestions.

### Product Deliverables

- User can match any analysed resume against any analysed job description.
- Match results show a score (0–100), a skill gap table with importance levels, and ordered improvement suggestions.
- Previous match results are stored and retrievable — running the same combination twice returns the cached result.
- Match history page lists all past matches.

### Backend Deliverables

- Sprint 3 EF migration: `match_results` table with a unique index on `(resume_id, job_description_id)`.
- Matching module: `MatchResult` entity, `MatchingService`, `MatchResultDto`.
- `MatchingService.MatchAsync` — validates that both resume and JD have been analysed before calling AI; returns `422` with clear error codes (`RESUME_NOT_ANALYSED`, `JD_NOT_ANALYSED`) if not.
- Idempotency: a second call with the same `resumeId` + `jobDescriptionId` returns the existing result without a new AI call.
- `POST /api/matches` — `{ resumeId, jobDescriptionId }`.
- `GET /api/matches/{id}`.
- `GET /api/matches` — list for the authenticated user.

### Frontend Deliverables

- `/matches/new` page: dropdowns to select resume and JD. Items without analysis are shown but disabled with a tooltip.
- Match results page `/matches/{id}`: score ring (SVG, colour-coded by threshold), skill gap table with importance badges, improvement suggestion cards.
- `/matches` list page: table with resume name, JD title, score badge, and date.
- Reusable score ring component — used here and on the dashboard in Sprint 4.

### Testing Deliverables

- Unit tests for `MatchingService`: missing resume analysis returns failure, missing JD analysis returns failure, idempotency (AI not called on second request with same ids).
- Integration tests: valid match returns `201`, same ids returns `200` with no new DB row, unanalysed resume returns `422`.
- Manual test: real resume + real JD → score and gaps are plausible.

### Acceptance Criteria

- Analyse a resume → analyse a JD → run match → score, gaps, and suggestions displayed.
- Running the same match twice does not trigger a second AI call.
- Selecting an unanalysed resume disables the match button with a clear message.
- Match history page shows all past results with correct scores.
- `GET /api/matches/{id}` returns `404` for a match belonging to another user.

---

## Sprint 4 — Job Tracking and Dashboard

**Weeks 7–8**

### Goal

Complete the product feature set. Users can track job applications through a status pipeline and view a summary of all activity on a dashboard. The MVP is feature-complete at the end of this sprint.

### Product Deliverables

- User can create a job application with company name, job title, status, notes, and an optional link to an existing JD.
- Application status pipeline: `saved → applied → interviewing → offer | rejected`.
- Status changes are recorded automatically as timeline events. Users can also add manual notes.
- Dashboard shows counts of resumes, JDs, matches, and applications, plus the user's average match score and recent activity.

### Backend Deliverables

- Sprint 4 EF migration: `job_applications` and `application_timeline_events` tables.
- JobTracking module: `JobApplication` entity, `ApplicationTimelineEvent` entity, `JobApplicationService`.
- Status transition: any status to any status is permitted (no forced sequence in MVP). Every status change appends a `status_change` timeline event automatically.
- Soft validation: invalid status string returns `422` with code `INVALID_STATUS`.
- `GET /api/applications` with optional `?status=` filter.
- `POST /api/applications`, `GET /api/applications/{id}`, `PUT /api/applications/{id}`, `DELETE /api/applications/{id}`.
- `POST /api/applications/{id}/timeline` — append a manual note event.
- `GET /api/dashboard/summary` — single query returning counts, average match score, recent matches (3), and recent applications (3). Target response time under 200 ms.

### Frontend Deliverables

- `/applications` page: applications grouped by status column (Saved, Applied, Interviewing, Offer, Rejected). Status updated via inline dropdown with optimistic update.
- `/applications/new` form: company name, job title, status, notes, optional JD link.
- `/applications/{id}` detail page: all fields plus timeline events sorted newest first, with an "Add Note" inline input.
- `/dashboard` page: stat cards, average score ring (reused from Sprint 3), recent matches table, recent applications table.
- Nav updated to include Applications and Dashboard links.

### Testing Deliverables

- Unit tests for `JobApplicationService`: status change appends timeline event, invalid status returns failure, ownership check returns `404` for other user's application.
- Integration tests for all five application endpoints.
- Integration test for `GET /api/dashboard/summary`: seeded user with known data asserts correct counts and average.
- Manual: `EXPLAIN ANALYZE` on the dashboard query confirms no sequential scan on large tables.

### Acceptance Criteria

- Create three applications in different statuses → grouped correctly on the board.
- Change status via dropdown → card updates immediately (optimistic), new timeline event visible on detail page.
- Add a manual note → appears in timeline without page refresh.
- Dashboard shows correct counts immediately after creating or deleting data.
- `GET /api/dashboard/summary` returns under 200 ms against a seeded dataset.

---

## Sprint 5 — Azure Deployment and CI/CD

**Weeks 9–10**

### Goal

Deploy the feature-complete MVP to Azure. Establish a CI/CD pipeline with automated staging deploys and a gated production deploy. The product is live and accessible at a real URL.

### Infrastructure Deliverables

- Azure resource groups for staging and production (`aijobcoach-staging-rg`, `aijobcoach-prod-rg`).
- Azure App Service (B1 or B2 based on cost and performance) per environment.
- Azure Database for PostgreSQL Flexible Server per environment (Burstable for staging).
- Azure Blob Storage with containers: `resumes-staging`, `resumes-prod`.
- Staging App Service deployment slot on the production App Service for blue/green swap.
- All secrets stored as Azure App Service environment variables — no secrets in code or git.
- Custom domain with Azure-managed TLS certificate. HTTPS enforced, HTTP redirected.
- `docs/AZURE_INFRA.md` documenting resource names, tiers, and secret rotation steps.

### CI/CD Deliverables

**`ci.yml`** — runs on every PR to `develop` or `main`:
- `dotnet build`, `dotnet test` (excludes `ManualOnly`, `AIIntegration`, `AzureIntegration` categories)
- `npm run build`, `npm run lint`
- `dotnet list package --vulnerable` — fails on any Critical severity finding
- `npm audit --audit-level=critical`

**`deploy-staging.yml`** — runs on merge to `develop`:
- All CI checks
- `dotnet ef database update` against staging PostgreSQL (automatic)
- Deploy to staging slot
- Smoke test: `GET /health/ready` must return 200

**`deploy-production.yml`** — runs on merge to `main`:
- All CI checks
- Manual approval gate via GitHub Environment `production` (required reviewer)
- After approval: `dotnet ef database update` against production PostgreSQL
- Deploy to production slot
- Smoke test: `GET /health/ready` must return 200
- Slot swap: staging → production (zero-downtime)

Branch protection: `main` and `develop` require passing CI and PR review before merge.

### Testing and Deployment Checks

- Merge a trivial commit to `develop` → staging deploys automatically, URL returns 200.
- Introduce a deliberate build failure → CI blocks merge.
- Merge to `main` → approval prompt appears, approve → production deploys.
- Reject approval → workflow stops, production unchanged.
- Introduce a migration error → workflow fails before deployment proceeds.

### Acceptance Criteria

- `https://{production-domain}` serves the application with a valid TLS certificate.
- `http://` permanently redirects to `https://`.
- A PR with a broken test cannot be merged to `develop` or `main`.
- Staging deploy runs without manual intervention on merge to `develop`.
- Production deploy requires explicit approval before any migration or traffic swap occurs.
- EF migrations never run directly against production from a local machine.
- `docs/RELEASE.md` documents the release and rollback procedure.

---

## Sprint 6 — Observability, Security Hardening, and Production Readiness

**Weeks 11–12**

### Goal

Make the production deployment observable, defensible, and signed off. The MVP is complete when this sprint's acceptance criteria are met.

### Observability Deliverables

- Application Insights connected to the API via `ApplicationInsights__ConnectionString`.
- Serilog `ApplicationInsights` sink: all `Information`-level and above logs shipped.
- Every HTTP request logged with method, path, status code, and duration — visible in the Requests blade.
- Every AI call logged as a dependency trace with duration and outcome.
- Every EF Core query appears as a dependency trace.
- Custom metrics via `TelemetryClient`: AI call latency, input/output token count per operation, match score on every result.
- Application Insights dashboard pinned with: request rate, error rate, average response time, AI call latency p95.
- Alert rule: error rate (5xx / total) > 1% over 5 minutes → email notification.

### Security Deliverables

- CORS locked to the production frontend origin only (`CORS__AllowedOrigin` env var). Wildcard never used in non-Development.
- Security headers on all responses: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Strict-Transport-Security` (max-age 1 year).
- Rate limiting on AI endpoints (`POST /api/resumes/{id}/analyze`, `POST /api/job-descriptions`, `POST /api/matches`): 10 requests per authenticated user per hour. Exceeded limit returns `429` with `Retry-After` header. Implemented with ASP.NET Core's built-in `RateLimiter`.
- Dependency vulnerability scan in CI for both backend (`dotnet list package --vulnerable`) and frontend (`npm audit`) — both already in Sprint 5 CI; verified to fail on a known vulnerable package.
- `docs/SECURITY.md`: secret rotation procedure, vulnerability response process.

### Health Check Deliverables

- `GET /health/live` — liveness probe: returns `200` if the process is running.
- `GET /health/ready` — readiness probe: returns `200` when PostgreSQL, Azure Blob Storage, and OpenAI are all reachable; returns `503` with per-check status when any dependency is unhealthy.
- Both endpoints registered with Azure App Service health check monitoring.
- Custom JSON response writer (not the default HTML format).

### Acceptance Criteria

- Five HTTP requests to production → all five visible in Application Insights Requests blade within 2 minutes.
- Trigger a 500 error → appears in Application Insights Failures blade with a full stack trace.
- AI call triggered → dependency trace with latency and token count logged.
- Match score recorded → visible in Metrics Explorer as a custom metric.
- `GET /health/ready` returns `200` when all dependencies are healthy.
- Stop PostgreSQL locally → `GET /health/ready` returns `503` with `db: Unhealthy`.
- Security headers present on all responses (verified in browser DevTools).
- Request from an unlisted origin returns `403`.
- 11th AI request within an hour from the same user returns `429` with `Retry-After`.
- Test the alert rule: generate 10 artificial 500 errors → email notification received within 10 minutes.
- End-to-end smoke test on production: register → upload resume → analyse → create JD → analyse → match → create application → view dashboard — all steps complete without errors in Application Insights.
- `README.md` updated with live URL, tech stack summary, architecture overview, and setup instructions.

---

## Definition of MVP Done

The MVP is complete when all of the following are true:

1. A new user can register, log in, upload a resume, extract text, and analyse it.
2. A user can paste a job description and analyse it.
3. A user can match a resume against a job description and receive a score, skill gaps, and suggestions.
4. A user can create and track job applications through a status pipeline.
5. The dashboard displays accurate summary data.
6. The application is deployed to a custom domain on Azure with a valid TLS certificate.
7. A PR with a failing test cannot be merged. Staging deploys automatically on `develop` merge. Production requires manual approval.
8. Application Insights shows live request traces, dependency traces, and exception telemetry.
9. Security headers are present, CORS is locked, and rate limiting is active on AI endpoints.
10. `GET /health/ready` returns `200` in production under normal operating conditions.
11. `README.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, and `docs/ROADMAP.md` are current and accurate.

---

## Post-MVP Ideas

These are not planned for the MVP and must not be scaffolded during the current development phase.

**Product features**
- Mock interview: AI-generated questions from JD + resume, free-text answers, AI feedback.
- Resume editor and builder with AI-assisted rewrites.
- Company research integration (Glassdoor, LinkedIn data).
- Email notifications for application reminders and status updates.
- Saved job search queries with new match alerts.

**Platform**
- OAuth login (Google, GitHub).
- Payment and subscription tiers (free tier with limits, paid tier for unlimited AI calls).
- Multi-tenancy for career coaches managing multiple users.
- Team or cohort views (bootcamp or university use case).

**Engineering**
- Background job queue (`BackgroundService` or Hangfire) for long-running AI calls with polling.
- Full CSRF token protection (beyond `SameSite=Lax`) before public production launch.
- Azure Key Vault for secret management.
- `pgvector` for semantic similarity search across resumes or matches.
- Per-environment feature flags.
- A/B testing framework for prompt variants.

---

## Scope Control Rules

These rules apply for the duration of the MVP build. When a new idea arises, apply these checks before acting on it.

1. **If it is not in the MVP scope, it does not get scaffolded.** No placeholder folders, no stub endpoints, no "we'll fill this in later" DTOs.
2. **If a feature is not needed this sprint, it does not get built this sprint.** Sprint boundaries exist to enforce focus.
3. **If a new idea arrives during a sprint, add it to the post-MVP list and continue.** Ideas are cheap; shipping is not.
4. **If a ticket is taking longer than expected, cut scope within the ticket.** Deliver the working core behaviour; defer the polish.
5. **If something can be deferred without breaking the user flow, defer it.** Optimistic UI, Kanban drag-and-drop, and rich text editors are not blockers.
6. **No infrastructure component is added without a concrete, current need.** Message queues, vector databases, and agent frameworks require a proven problem first.
7. **Production hardening is Sprint 5–6 work.** Avoid adding observability, rate limiting, or security headers to earlier sprints — those sprints have their own goals.

---

*Last updated: Sprint 1 in progress — auth updated from localStorage JWT to HttpOnly cookie; Post-MVP engineering list updated accordingly.*
*Update this document at the start of each sprint to reflect what was completed, what changed, and what is next.*
