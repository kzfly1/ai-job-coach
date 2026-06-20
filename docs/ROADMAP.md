# AI Job Coach — Sprint Roadmap

This document describes the planned work for Sprints 2–8 and the Release Readiness milestone. It is intentionally higher-level than sprint ticket files — it gives enough context to understand the goal and acceptance bar for each sprint without duplicating ticket detail.

For architectural decisions, see `docs/DECISIONS.md`. For implementation conventions, see `docs/PROJECT_CONTEXT.md` and `docs/ARCHITECTURE.md`.

---

## Product Tracks

**Track A — Application Copilot:** Help users get more interviews.
Workflow: Resume → JD Analysis → Resume Match → Resume Suggestions → Tailored Resume → Apply.

**Track B — Career Intelligence:** Help users improve over time.
Workflow: Applications → Interview Journal → AI Reflection → Career Insights Dashboard.

The goal is not to optimise ATS scores. The goal is to help software engineers get interviews and eventually receive offers.

---

## Current Foundation (Sprint 1 — Complete)

**Backend:** ASP.NET Core Web API with one `AppDbContext`, SharedKernel (`Result<T>`, `Error`, `Guard`), Serilog structured logging, GlobalExceptionHandler, and ResultMapper. Auth module (custom JWT + BCrypt, HttpOnly cookie auth). Resumes module with `IFileStorageService`, `LocalFileStorageService`, full CRUD including soft delete. EF InitialCreate migration with `users` and `resumes` tables.

**Frontend:** Next.js App Router, TypeScript strict mode, TanStack Query v5, shadcn/ui. `apiClient` with `credentials: 'include'`, `useAuth()` backed by `GET /api/auth/me`, HttpOnly cookie auth flow, register and login pages, protected dashboard layout, resume upload page (XHR progress), resume list page.

**Current user journey:** Register → Login → Upload Resume → View Resumes.

**What is not yet done:** text extraction, AI calls, JD analysis, matching, job search, resume tailoring, interview preparation, application tracking, career insights, Azure Blob Storage, CI/CD, and production observability.

---

## Production-Readiness Approach

Production-readiness tasks are woven into each sprint where the relevant feature ships, not deferred to a dedicated infrastructure sprint. Each sprint section below lists the specific production-readiness tasks that belong to it.

**Full Release Readiness** (Azure Blob Storage, CI/CD pipelines, Application Insights, security headers, health checks, CORS hardening) is documented as a separate milestone after the core AI workflows are validated. It has no fixed sprint assignment — it is planned once the feature set is stable enough to deploy.

See the **Release Readiness Milestone** section at the bottom of this document.

---

## Sprint 2 — Resume Intelligence and JD Intelligence

**Goal:** Build the two structured inputs required for matching. By the end of this sprint, the system understands both sides of the Resume ↔ Job equation. The user can upload a resume and receive a Developer Profile, and paste a job description and receive a structured Job Profile.

**LocalFileStorageService remains active throughout this sprint.** Azure Blob Storage is not part of Sprint 2 scope.

### Product Deliverables

- Resume text extracted on upload (PDF and DOCX). Extraction failure does not block the upload.
- User triggers AI analysis of a resume and views a Developer Profile: skills, programming languages, frameworks, cloud platforms, databases, years of experience, career level, career summary, and projects.
- User pastes a Job Description, saves it, and views a structured Job Profile: required skills, preferred skills, responsibilities, experience requirements, and role type.
- Resume list shows extraction status and whether a Developer Profile exists.

### Backend Deliverables

- `ResumeTextExtractor` using `PdfPig` (PDF) and `DocumentFormat.OpenXml` (DOCX). Extraction failure sets `content_text = null` and logs a Warning — it does not fail the upload.
- Sprint 2 EF migration: `resume_analyses` and `job_descriptions` tables.
- AI module (`Modules/AI/`): `IOpenAIClient` interface, `OpenAIClient` implementation (named `HttpClient` with Polly retry: 3 attempts, exponential backoff, 30-second timeout), `PromptLoader` reading from `Modules/AI/Prompts/`.
- `ResumeAnalysisService.AnalyseAsync` — sends extracted text to OpenAI with structured schema, upserts result into `resume_analyses`.
- `JobDescriptions` module: `JobDescription` entity, `JobDescriptionService.AnalyseAsync`, `JobDescriptionsController`.
- Endpoints: `POST /api/resumes/{id}/analyse`, `GET /api/resumes/{id}/analysis`, `POST /api/job-descriptions`, `GET /api/job-descriptions`, `GET /api/job-descriptions/{id}`, `DELETE /api/job-descriptions/{id}`.
- OpenAI config validated at startup: app refuses to start if `OpenAI__ApiKey` is absent in non-Development.

**Production-readiness tasks woven into Sprint 2:**
- OpenAI timeout (30 seconds) and Polly retry policy configured on the named HttpClient.
- Structured Serilog logging on every AI call: `PromptName`, `Model`, `LatencyMs`, `ResumeId`, `Status` as separate properties (not interpolated strings).
- `ResultMapper` extended with `CONTENT_TEXT_MISSING → 422`, `AI_UNAVAILABLE → 503`, `AI_PARSE_ERROR → 502`.

### Frontend Deliverables

- Resume detail page (`/resumes/[id]`): extraction status, "Analyse Resume" button, loading state during analysis, Developer Profile rendered on success, re-analyse button when profile exists.
- Developer Profile component: career level badge, years of experience, career summary, skill sections (Programming Languages, Frameworks, Cloud Platforms, Databases, Tools) as Badge lists, project cards. Empty sections hidden.
- `/jobs/new` page: paste JD with title and optional company. Submits to `POST /api/job-descriptions`.
- `/jobs` list page: table of saved JDs with title, company, and date.
- JD detail view: structured requirements, skills, and role type displayed after analysis.
- Resume list page updated: analysis status badge per resume (Profile Ready / Not Analysed / Extraction Failed).

### Testing Deliverables

- Unit tests: `ResumeTextExtractor` (valid PDF, valid DOCX, corrupt file returns `null`), `PromptLoader` (placeholder substitution, error on missing key), `OpenAIClient` (retry fires on 500, `OpenAIParseException` on malformed JSON), `ResumeAnalysisService` (null `content_text` → `CONTENT_TEXT_MISSING`, upsert behaviour).
- Integration tests: `POST /api/resumes/{id}/analyse` (success, 422 on null content, 503 on OpenAI unreachable), `POST /api/job-descriptions` (success with analysis populated).
- AI integration tests marked `[Category("AIIntegration")]` — run manually, excluded from CI.

### Acceptance Criteria

- Upload a real PDF → `content_text` populated → click Analyse → Developer Profile displayed.
- Upload a corrupt file → upload succeeds → extraction failed badge shown → no Analyse button.
- Paste a real JD → Job Profile with skills and requirements displayed.
- Re-analyse a resume → existing profile updated in place without page reload.
- `POST /api/resumes/{id}/analyse` with null `content_text` → 422, code `CONTENT_TEXT_MISSING`.
- OpenAI call exceeding 30 seconds → request cancelled → 503 returned → Warning logged with `LatencyMs`.

---

## Sprint 3 — Resume Matching, Match Report, and Resume Suggestions

**Goal:** Deliver the first end-to-end value loop. A user can compare their Developer Profile against a Job Profile and receive a Match Report: score, strengths, missing skills, skill gaps, and concrete resume improvement suggestions.

### Product Deliverables

- User selects an analysed resume and an analysed job description and runs a match.
- Match Report shows: match score (0–100), strengths, missing skills, skill gap table with importance levels, and ordered resume improvement suggestions.
- Running the same resume + JD combination twice returns the cached result — no duplicate AI call.
- Match history page lists all past match reports.

### Backend Deliverables

- Sprint 3 EF migration: `match_results` table with unique index on `(resume_id, job_description_id)`.
- `Matching` module: `MatchResult` entity, `MatchingService`, `MatchResultDto`.
- `MatchingService.MatchAsync`: validates both inputs are analysed, calls OpenAI with both profiles, parses structured result. Returns `422` with `RESUME_NOT_ANALYSED` or `JD_NOT_ANALYSED` if prerequisites are missing.
- Idempotency: second call with same IDs returns existing result, no new AI call.
- Resume Suggestions are a first-class output field in `MatchResult`, not an afterthought.
- Endpoints: `POST /api/matches`, `GET /api/matches/{id}`, `GET /api/matches`.
- `ResultMapper` extended with `RESUME_NOT_ANALYSED → 422`, `JD_NOT_ANALYSED → 422`.

**Production-readiness tasks woven into Sprint 3:**
- Rate limiting on `POST /api/matches`: 10 requests per authenticated user per hour (ASP.NET Core built-in `RateLimiter`, sliding window). Returns 429 with `Retry-After` header.
- Match score logged as a structured Serilog property on every result.

### Frontend Deliverables

- `/matches/new` page: dropdowns for resume and JD. Unanalysed items shown but disabled with tooltip.
- Match results page (`/matches/[id]`): score ring (SVG, colour-coded by threshold), strengths section, skill gap table with importance badges, improvement suggestion cards.
- `/matches` list page: table with resume name, JD title, score badge, and date.
- Reusable score ring component (used here and in later dashboards).

### Testing Deliverables

- Unit tests: `MatchingService` (missing analysis → correct failure codes, idempotency — AI not called on second request, suggestions parsed correctly).
- Integration tests: valid match → 201, same IDs → 200 (no new DB row), unanalysed resume → 422.
- Manual test: real resume + real JD → score, gaps, and suggestions are plausible.

### Acceptance Criteria

- Analyse resume → analyse JD → run match → score, gaps, strengths, and suggestions displayed.
- Running the same match twice does not trigger a second AI call.
- 11th match request within one hour → 429 with `Retry-After` header.
- `GET /api/matches/{id}` returns 404 for a match belonging to another user.

---

## Sprint 4 — Job Search Integration

**Goal:** Surface relevant job opportunities directly inside the product, each showing a match score against the user's active resume. Users stop searching externally and start evaluating fit immediately.

### Product Deliverables

- User searches for jobs by keyword and location.
- Results show job cards: company, job title, location, posted date, and match score against their active (most recently analysed) resume.
- User can open a job card and trigger JD analysis to generate a full Job Profile.
- User can initiate a match from a job card directly.

### Backend Deliverables

- `JobSearch` module: `JobSearchService`, `JobSearchController`.
- Integration with one external job API (JSearch via RapidAPI, or Adzuna — decided at sprint start based on API availability and cost). One provider only for MVP.
- External API called with timeout (10 seconds) and basic retry (2 attempts). Failure returns 503.
- Job search results cached in memory for 15 minutes per query to avoid redundant external API calls.
- Match score on job cards: computed on-demand by calling `MatchingService` with the user's active resume. Active resume = most recently analysed resume for the user.
- Endpoints: `GET /api/job-search?query=&location=&page=`, `POST /api/job-search/{externalJobId}/analyse` (saves JD and runs analysis).

**Production-readiness tasks woven into Sprint 4:**
- External API timeout and retry configured on the named HttpClient for the job search provider.
- API key for external job search provider validated at startup.
- Rate limiting on `GET /api/job-search`: 20 requests per user per hour.

### Frontend Deliverables

- `/jobs/search` page: search form (keyword, location), job card grid with match score badge, pagination.
- Job card: company logo (if available), job title, company name, location, posted date, match score ring, "View Details" link.
- Job detail drawer or page: full JD text, "Analyse JD" button, "Run Match" button.

### Testing Deliverables

- Unit tests: `JobSearchService` (external API timeout → 503, results cached correctly, match score computed against active resume).
- Integration tests: search endpoint with mocked external API → correct response shape.
- Manual test: real job search query → results with match scores displayed.

### Acceptance Criteria

- Search for "backend engineer Sydney" → job cards returned with match scores.
- Match score on card reflects the most recently analysed resume.
- External API unavailable → 503 returned, error message shown in UI.
- Same search within 15 minutes → cached result returned, no external API call.

---

## Sprint 5 — Resume Tailoring and Versioning

**Goal:** Reduce the effort of preparing tailored job applications. Given a resume and a job description, the system generates tailored resume content aligned to the role. The user reviews the suggestions and downloads a formatted document.

### Product Deliverables

- User selects a resume and a job description and requests a tailored version.
- System generates: rewritten bullet points aligned to the JD, keyword-optimised summary, achievement rewriting suggestions, skills section adjustments.
- User reviews changes before accepting.
- User downloads a formatted DOCX or PDF using a controlled template (not preserving the original resume's exact layout — out of scope).
- Tailored versions are stored and linked to the original resume and target JD.
- User can view version history: original resume + all tailored versions with their target job.

### Backend Deliverables

- Sprint 5 EF migration: `resume_versions` table.
- `ResumeTailoring` module: `ResumeTailoringService`, `ResumeVersionEntity`, `ResumeTailoringController`.
- `ResumeTailoringService.TailorAsync`: sends resume text + JD text to OpenAI with tailoring prompt. Returns structured tailoring suggestions, not a raw document.
- Document generation: use `DocumentFormat.OpenXml` with a controlled template to produce a downloadable DOCX. Template defines layout — original resume formatting is not replicated.
- Generated documents stored via `IFileStorageService` (local in Development, Azure Blob when configured).
- Endpoints: `POST /api/resume-tailoring` (`{ resumeId, jobDescriptionId }`), `GET /api/resume-tailoring/{id}`, `GET /api/resume-tailoring/{id}/download`, `GET /api/resumes/{id}/versions`.

**Out of scope for Sprint 5:**
- Preserving the original uploaded resume's exact layout.
- Editing the original PDF/DOCX in place.
- Real-time collaborative editing of the tailored content.

**Production-readiness tasks woven into Sprint 5:**
- Rate limiting on `POST /api/resume-tailoring`: 5 requests per user per hour (tailoring is more expensive than analysis).
- Document generation failures logged with structured properties; user receives 500 with `DOCUMENT_GENERATION_FAILED` code.

### Frontend Deliverables

- `/tailoring/new` page: select resume and JD, submit tailoring request.
- Tailoring result page: side-by-side view of original content and suggested changes. User can accept or reject individual sections.
- Download button: generates and downloads the DOCX using the controlled template.
- `/resumes/[id]/versions` page: version history list with target job and date.

### Testing Deliverables

- Unit tests: `ResumeTailoringService` (null inputs → correct failures, suggestion parsing, upsert behaviour).
- Unit tests: document generation (controlled template produces valid DOCX with expected sections).
- Integration tests: `POST /api/resume-tailoring` → 201 with suggestion content, download endpoint → DOCX file returned.

### Acceptance Criteria

- Select a resume and a JD → tailored suggestions generated → user reviews and downloads DOCX.
- Downloaded DOCX opens correctly in Microsoft Word and Google Docs.
- Version history shows the tailored resume linked to the correct job.
- Rate limit: 6th tailoring request within one hour → 429.

---

## Sprint 6 — Interview Preparation (including Company Intelligence)

**Goal:** Help users prepare for interviews by generating role-specific questions and a lightweight company brief. Company Intelligence is a sub-feature of interview preparation — it provides context that feeds directly into question generation and preparation notes. It is not a standalone product.

### Product Deliverables

- User selects a job (from a match or from their saved JDs) and generates an interview preparation package.
- Package includes:
  - **Company Brief** (Company Intelligence sub-feature): company overview, industry summary, mission and values, business model summary, relevant recent news if available via AI knowledge, possible tech stack signals.
  - **Question Bank**: behavioral questions, technical questions, role-specific questions (Backend / Frontend / Full Stack / Cloud / AI Engineer role types).
  - **Preparation Notes**: key topics to study, suggested talking points based on the user's resume.
- Company Brief is lightweight. It is not a full company research platform and does not call external company data APIs.

### Backend Deliverables

- `InterviewPreparation` module: `InterviewPrepService`, `CompanyIntelligenceService` (within the same module), `InterviewPrepController`.
- `CompanyIntelligenceService.GenerateBriefAsync`: single OpenAI call with company name and JD context. Returns structured brief.
- `InterviewPrepService.GeneratePackageAsync`: orchestrates Company Brief + Question Bank generation. Two AI calls (company brief, questions) in sequence.
- Sprint 6 EF migration: `interview_prep_sessions` table.
- Endpoints: `POST /api/interview-prep` (`{ jobDescriptionId }`), `GET /api/interview-prep/{id}`.

**Production-readiness tasks woven into Sprint 6:**
- Rate limiting on `POST /api/interview-prep`: 5 requests per user per hour.
- Both AI calls within a prep session logged as separate structured events.

### Frontend Deliverables

- `/interview-prep/new` page: select a saved JD, submit.
- Interview prep result page: tabbed layout — Company Brief tab, Questions tab, Preparation Notes tab.
- Company Brief tab: overview card, industry badge, key values list, news items (if available).
- Questions tab: behavioral and technical questions grouped by type, each expandable with guidance notes.
- Preparation Notes tab: key topics and talking points.

### Testing Deliverables

- Unit tests: `CompanyIntelligenceService` (brief parsing, missing company name → graceful fallback), `InterviewPrepService` (orchestration, partial failure handling if one AI call fails).
- Integration tests: `POST /api/interview-prep` → 201 with package content.
- Manual test: real JD → company brief and questions are relevant and accurate.

### Acceptance Criteria

- Select a saved JD → interview prep package generated with company brief and questions.
- Company Brief shows overview, industry, and values.
- Questions are divided into behavioral and technical sections.
- If company name is not determinable from JD → brief shows a graceful fallback rather than an error.

---

## Sprint 7 — Application Tracking and Interview Journal

**Goal:** Give users a place to manage their active job search and capture interview learnings before they forget them. Application Tracking and Interview Journal are tightly coupled — journal entries are attached to applications.

### Product Deliverables

- User creates a job application record with company name, job title, status, notes, and optional link to a saved JD or tailored resume version.
- Application status pipeline: Saved → Applied → Interviewing → Offer | Rejected.
- Status changes recorded automatically as timeline events. Users can add manual notes.
- User records interview journal entries attached to an application: stage, questions asked, feedback received, personal reflection.
- Journal entries are private and local to the user.

### Backend Deliverables

- Sprint 7 EF migration: `job_applications`, `application_timeline_events`, `interview_journal_entries` tables.
- `JobTracking` module: `JobApplication` entity, `ApplicationTimelineEvent` entity, `JobApplicationService`, `JobTrackingController`.
- `InterviewJournal` concern within `JobTracking` module (separate service and controller if complexity warrants, otherwise co-located): `InterviewJournalEntry` entity, `InterviewJournalService`.
- Endpoints: `GET /api/applications`, `POST /api/applications`, `GET /api/applications/{id}`, `PUT /api/applications/{id}`, `DELETE /api/applications/{id}`, `POST /api/applications/{id}/timeline`, `GET /api/applications/{id}/journal`, `POST /api/applications/{id}/journal`.

### Frontend Deliverables

- `/applications` page: applications grouped by status (Saved, Applied, Interviewing, Offer, Rejected). Status updated via inline dropdown with optimistic update.
- `/applications/new` form: company name, job title, status, notes, optional JD and tailored resume links.
- `/applications/[id]` detail page: fields, timeline events, and journal entries.
- Journal entry form: stage, questions asked, feedback, reflection (inline on detail page).

### Testing Deliverables

- Unit tests: `JobApplicationService` (status change → timeline event, invalid status → failure, ownership → 404), `InterviewJournalService` (entry creation, ownership checks).
- Integration tests for all application and journal endpoints.

### Acceptance Criteria

- Create three applications in different statuses → grouped correctly.
- Change status → timeline event created automatically.
- Add a journal entry to an application → appears on detail page without refresh.
- Journal entries of one user are not visible to another user.

---

## Sprint 8 — Career Insights Dashboard

**Goal:** Aggregate data from all prior modules into a dashboard that shows the user measurable progress and patterns. AI Reflection analyses the user's interview journal to identify repeated weaknesses and learning opportunities.

### Product Deliverables

- Dashboard metrics: application count, interview rate, offer rate, rejection breakdown by category, skill gap trends across matches.
- Most common weaknesses identified by AI from journal entry patterns.
- Learning recommendations based on repeated failure themes.
- Skill gap trend: which skills appear most often as missing across all match reports.

### Backend Deliverables

- Sprint 8 EF migration: none expected (reads from existing tables).
- `CareerInsights` module: `CareerInsightsService`, `AIReflectionService`, `CareerInsightsController`.
- `AIReflectionService.ReflectAsync`: sends aggregated journal history to OpenAI, returns structured failure themes and recommendations.
- `GET /api/career-insights/summary`: counts, rates, top skill gaps, recent activity.
- `POST /api/career-insights/reflect`: triggers AI Reflection on the user's journal history. Expensive — rate limited to 3 per user per day.

**Production-readiness tasks woven into Sprint 8:**
- Dashboard summary query: single DB round-trip with CTEs or projections. Target under 200 ms.
- AI Reflection rate limited to 3 per user per day (daily window, not hourly).

### Frontend Deliverables

- `/dashboard` page (replacing the placeholder from Sprint 1): stat cards (applications, interviews, offers, rejections), interview rate and offer rate as percentage rings, skill gap trend chart, recent activity feed.
- AI Reflection panel: "Analyse My Journey" button, loading state, reflection output (failure themes + recommendations).
- Skill gap trend: horizontal bar chart of top 5 missing skills across all matches.

### Testing Deliverables

- Integration test: `GET /api/career-insights/summary` with seeded data → correct counts and rates.
- Unit test: `AIReflectionService` (journal history parsed, structured output returned, empty history → graceful response).
- Manual: `EXPLAIN ANALYZE` on summary query confirms no sequential scan on large tables.

### Acceptance Criteria

- Dashboard shows correct counts immediately after creating or updating data.
- AI Reflection with real journal entries produces recognisable failure themes.
- Skill gap trend chart reflects actual patterns from match results.
- Summary query returns under 200 ms against a seeded dataset.

---

## Release Readiness Milestone

This milestone has no fixed sprint assignment. It is planned after the core AI workflows (Sprints 2–4) are validated and the feature set is stable enough to deploy to a production environment.

### Infrastructure

- Azure resource groups for staging and production.
- Azure App Service (B1 or B2) per environment.
- Azure Database for PostgreSQL Flexible Server per environment.
- Azure Blob Storage with containers per environment (`resumes-staging`, `resumes-prod`, `documents-staging`, `documents-prod`). `AzureBlobStorageService` activated via DI swap.
- Staging deployment slot for blue/green swap.
- Custom domain + Azure-managed TLS certificate. HTTPS enforced, HTTP redirected.
- All secrets in Azure App Service environment variables. No secrets in code or git.
- `docs/AZURE_INFRA.md`: resource names, tiers, secret rotation steps.

### CI/CD

- `ci.yml`: `dotnet build`, `dotnet test` (excluding `ManualOnly`, `AIIntegration`, `AzureIntegration`), `npm run build`, `npm run lint`, `dotnet list package --vulnerable`, `npm audit --audit-level=critical`. Runs on every PR to `develop` or `main`.
- `deploy-staging.yml`: CI checks + `dotnet ef database update` (automatic) + deploy to staging slot + smoke test (`GET /health/ready` → 200). Runs on merge to `develop`.
- `deploy-production.yml`: CI checks + manual approval gate (GitHub Environment: `production`) + migrations + deploy + smoke test + slot swap. Runs on merge to `main`.

### Observability

- Application Insights connected via Serilog sink.
- Every HTTP request logged with method, path, status, and duration.
- Every AI call logged as a dependency trace with latency and token count.
- Custom metrics: AI call latency, token usage per operation, match scores.
- Alert rule: error rate > 1% over 5 minutes → email notification.

### Security

- CORS locked to production frontend origin (`CORS__AllowedOrigin` env var).
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, HSTS.
- Full CSRF hardening reviewed before public launch (SameSite=Lax is acceptable until then).
- `docs/SECURITY.md`: secret rotation procedure, vulnerability response process.
- `docs/RELEASE.md`: release and rollback procedure.

### Health Checks

- `GET /health/live`: liveness probe.
- `GET /health/ready`: readiness probe checking PostgreSQL, Azure Blob Storage, and OpenAI reachability. Returns 503 with per-check status on dependency failure.

### Release Readiness Acceptance Criteria

- End-to-end smoke test on production: register → upload resume → analyse → paste JD → analyse → match → search jobs → tailor resume → prep interview → track application → view dashboard — all steps complete without errors.
- `README.md` updated with live URL, tech stack, architecture overview, setup instructions.
- All docs current: `ARCHITECTURE.md`, `DECISIONS.md`, `ROADMAP.md`.

---

## Definition of Done

The product is feature-complete when:

1. A user can upload a resume and receive a Developer Profile.
2. A user can paste a job description and receive a structured Job Profile.
3. A user can match resume against JD and receive a Match Report with score, gaps, and suggestions.
4. A user can search for jobs and see match scores against their active resume.
5. A user can generate a tailored resume version and download it as DOCX.
6. A user can generate an interview preparation package with company brief and role-specific questions.
7. A user can track job applications through a status pipeline with timeline events.
8. A user can record interview journal entries attached to applications.
9. A user can view a Career Insights Dashboard with AI Reflection on their interview history.

The product is production-ready when the Release Readiness Milestone acceptance criteria are met.

---

## Post-MVP

Items that are not in the current sprint plan and must not be scaffolded.

**Product**
- OAuth login (Google, GitHub).
- Payment and subscription tiers.
- Multi-tenancy for career coaches.
- Email notifications.
- Team or cohort views.
- Saved job search alerts.

**Engineering**
- Background job queue (flag for Sprint 4+ if external API calls become slow enough to warrant it).
- Full CSRF token implementation (SameSite=Lax acceptable until then).
- Azure Key Vault for secret management.
- `pgvector` for semantic similarity search.
- Per-environment feature flags.
- A/B testing framework for prompt variants.

---

## Scope Control Rules

1. Build only what the current sprint requires. No placeholder folders, no stub endpoints.
2. If a feature is not needed this sprint, it does not get built this sprint.
3. If a new idea arrives during a sprint, add it to the Post-MVP list and continue.
4. If a ticket is taking longer than expected, cut scope within the ticket — deliver the working core, defer the polish.
5. If something can be deferred without breaking the user flow, defer it.
6. No infrastructure component is added without a concrete, current need.
7. Production-readiness tasks (logging, timeout handling, rate limiting, config validation) are woven into the sprint where the relevant feature ships. Full Azure deployment and CI/CD are Release Readiness work, not sprint work.

---

*Last updated: Sprint 1 complete — Roadmap rewritten to reflect Roadmap v2 product direction (eight sprints, two product tracks, Release Readiness milestone replaces dedicated infra sprints, Azure Blob deferred out of Sprint 2).*
*Update this document at the start of each sprint to reflect what was completed, what changed, and what is next.*
