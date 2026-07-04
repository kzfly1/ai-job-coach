# AI Job Coach — AI Workflow

This document defines how AI coding agents should help develop AI Job Coach.

It does not replace `PROJECT_CONTEXT.md`, `ARCHITECTURE.md`, `DECISIONS.md`, or `ROADMAP.md`.
Those documents describe what the project is, how it is designed, why decisions were made, and what the roadmap is.
This document describes how an AI agent should execute a task safely.

---

## 1. Purpose

AI agents must behave like careful junior developers working under review.

The goal is not to generate the largest possible code change.
The goal is to make small, correct, reviewable changes that follow the project's architecture and sprint scope.

Core principles:

- Understand before coding.
- Classify the task before choosing a workflow.
- Keep each change inside the current ticket scope.
- Prefer small vertical slices over broad rewrites.
- Ask before changing architecture, database schema, authentication, deployment, or shared conventions.
- Verify before claiming work is done.

---

## 2. Source Documents

Before working on a ticket, read the documents in this order:

1. `PROJECT_CONTEXT.md`
   - Current sprint scope
   - Current implementation status
   - Hard constraints
   - Module boundaries
   - Allowed and prohibited patterns

2. `docs/ARCHITECTURE.md`
   - Backend module structure
   - Layer responsibilities
   - Controller style
   - Data access rules
   - Frontend architecture
   - Testing strategy

3. `docs/DECISIONS.md`
   - Accepted architectural decisions
   - Superseded decisions
   - Trade-offs and revisit triggers

4. `docs/ROADMAP.md`
   - Sprint goals
   - Product tracks
   - Acceptance criteria
   - Future scope boundaries

Use this document after reading the above files to decide how to work.

---

## 3. Task Classification

Before touching code, classify the task in one sentence.

### Track A — Clear Implementation Task

Use Track A when the task is already well-defined.

Examples:

- Bug fix
- Small UI polish
- Validation change
- Minor refactor
- Add a missing field
- Update an existing DTO
- Fix an IDE or TypeScript warning
- Add a small test for existing behavior

Track A workflow:

```txt
Understand
→ Spec
→ Build
→ Test
→ Review
→ Ship
```

### Track B — Design / Unclear Task

Use Track B when the task needs design decisions before implementation.

Examples:

- New feature
- New module
- New database table
- API contract design
- AI integration
- Multi-file restructuring
- Authentication behavior change
- Cross-module dependency
- Deployment or infrastructure change
- Anything that may affect architecture or roadmap scope

Track B workflow:

```txt
Understand
→ Surface Unknowns
→ Discuss Options
→ Propose Plan
→ Wait for Approval
→ Spec
→ Build Incrementally
→ Test
→ Review
→ Ship
```

### Rule

If unsure whether a task is Track A or Track B, treat it as Track B.

---

## 4. Start-of-Ticket Protocol

At the start of every ticket, the AI agent must respond with:

1. Task classification
   - Track A or Track B
   - One-sentence reason

2. Ticket understanding
   - What the ticket is trying to achieve
   - What user or engineering problem it solves

3. Scope boundary
   - What is included
   - What is explicitly excluded

4. Files to inspect
   - Existing files that must be read before editing

5. Expected files to change
   - Files likely to be modified or created

6. Implementation plan
   - Ordered steps
   - Small enough to review

7. Risks, assumptions, or questions
   - Anything that could change the implementation approach

Do not start coding until the plan is approved when the task is Track B or when the change touches architecture, database schema, auth, deployment, or shared conventions.

---

## 5. Implementation Rules

### General Rules

- Work on one ticket at a time.
- Do not mix unrelated refactors with feature work.
- Do not scaffold future modules before their sprint.
- Do not create empty placeholder folders.
- Do not introduce new libraries unless the ticket explicitly requires them.
- Do not change API contracts unless the ticket requires it.
- Do not change authentication behavior without explicit approval.
- Do not change database schema without explicit approval.
- Do not modify generated files unless the tool output is expected.
- Keep code readable over clever.

### Incremental Build Rule

For multi-step changes, implement in safe increments:

```txt
Domain / types
→ Application logic
→ Infrastructure wiring
→ Controller / API
→ Frontend API client
→ Hooks
→ UI
→ Tests
```

Not every ticket needs every layer.

---

## 6. Backend Workflow

Use this workflow for backend tickets.

### Step 1 — Identify Layer Ownership

Before implementation, state which layer owns the change:

- Domain
- Application
- Infrastructure
- Controller
- Data / EF migration
- SharedKernel
- Middleware
- Program / DI configuration

### Step 2 — Enforce Layer Boundaries

Backend rules:

- Controllers are thin HTTP adapters.
- Controllers extract `userId` from claims and pass `Guid` to services.
- Application services contain business workflow.
- Domain entities contain business state and behavior only.
- Domain entities must not depend on HTTP, EF attributes, infrastructure, or DTOs.
- Infrastructure contains EF configurations and external adapters.
- Application services may inject `AppDbContext` directly.
- Use manual mapping.
- Use `Result<T>` for expected business failures.
- Let `GlobalExceptionHandler` handle unexpected failures.

### Step 3 — Database Change Protocol

For schema changes:

1. Explain why a schema change is required.
2. Define the entity and relationship.
3. Define indexes and constraints.
4. Confirm whether the migration belongs to the current sprint.
5. Generate the migration only after approval.
6. Inspect the migration before applying it.

Never add tables for future sprints.

### Step 4 — Backend Done Gate

A backend task is not done until:

- `dotnet build` passes.
- `dotnet test` passes, or skipped tests are explained.
- Relevant service tests are added or updated.
- Result/error codes are mapped correctly if new failures are introduced.
- Ownership checks are present where user data is accessed.
- CancellationToken is passed through async service calls where appropriate.
- No prohibited patterns were introduced.

---

## 7. Frontend Workflow

Use this workflow for frontend tickets.

### Step 1 — Classify State

Before implementation, classify state into:

- Server state
  - Data from backend
  - Managed by TanStack Query

- Form state
  - User input
  - Managed by React Hook Form + Zod

- UI state
  - Dialog open state
  - Selected item
  - Dragging state
  - Managed by local component state

Do not put server state into `useState` unless there is a clear reason.

### Step 2 — Component Ownership

Use this structure by default:

```txt
page.tsx
→ thin route entry

Feature container
→ query, mutation, orchestration, state branching

Presentation components
→ display only, receive props

Hooks
→ reusable server state and mutation logic

API client files
→ HTTP calls only
```

### Step 3 — Client Component Boundary

Use `"use client"` only at the necessary boundary.

Prefer one feature-level client boundary instead of marking every child component as client.

Example:

```txt
ResumeList.tsx                  → "use client"
ResumeListItem.tsx              → no "use client"
ResumeDeleteDialog.tsx          → no "use client" unless required
ResumeListEmptyState.tsx        → no "use client"
```

If a child component directly uses hooks, browser APIs, or event handlers and is imported from a server component, then it needs `"use client"`.

### Step 4 — Frontend Done Gate

A frontend task is not done until:

- `npx tsc --noEmit` passes.
- `npm run lint` passes.
- `npm run build` passes when the task affects routing, layout, or Next.js behavior.
- Loading, error, empty, and success states are handled.
- API calls go through typed API wrappers.
- Server state uses TanStack Query.
- Mutations invalidate or update the correct query keys.
- Components remain reasonably small and readable.
- No raw `fetch()` is added inside components.

---

## 8. AI Feature Workflow

Use this workflow for OpenAI / LLM-related tickets.

### Step 1 — Keep AI Operations Narrow

Each MVP AI operation should be a single structured operation:

- Resume analysis
- JD analysis
- Resume-to-JD matching
- Resume tailoring
- Interview preparation
- Career reflection

Do not introduce agent frameworks, vector databases, autonomous tool use, or multi-step agents unless a future sprint explicitly requires them.

### Step 2 — Prompt Management

Prompts must live as plain text files, not hardcoded C# strings.

Prompt rules:

- Return valid JSON only.
- No markdown fences.
- No explanatory text outside JSON.
- Include the expected JSON schema.
- Keep prompt inputs explicit.
- Keep prompt output stable for parsing.

### Step 3 — AI Error Handling

AI integrations must handle:

- Timeout
- Provider unavailable
- Malformed JSON
- Empty response
- Missing required fields
- Cancellation

Expected failures should become explicit `Result<T>` errors where appropriate.

### Step 4 — AI Done Gate

An AI task is not done until:

- Prompt file exists and is readable.
- Prompt placeholders are tested.
- Response parsing is tested.
- Malformed response behavior is tested.
- Timeout and unavailable provider behavior are handled.
- Raw model response is logged safely enough for debugging.
- Sensitive user data is not logged unnecessarily.

---

## 9. Review Checklists

### Backend Review Checklist

Check:

- Controller is thin.
- Service owns business workflow.
- Domain is free of HTTP and EF attributes.
- DTO mapping is explicit.
- `AppDbContext` usage is simple and readable.
- No repository pattern introduced.
- No AutoMapper introduced.
- No MediatR or CQRS introduced.
- Error codes are consistent.
- ResultMapper is updated only for real new errors.
- User ownership checks are present.
- Tests cover important behavior.

### Frontend Review Checklist

Check:

- Page component is thin.
- Feature container owns query/mutation orchestration.
- Components are not over-abstracted.
- Server state uses TanStack Query.
- Forms use React Hook Form + Zod.
- API calls go through typed client functions.
- Loading, error, empty, success states exist.
- Mutation invalidation is correct.
- Client component boundaries are minimal.
- No unnecessary global state.

### Database Review Checklist

Check:

- Migration belongs to the current sprint.
- Table name follows existing naming.
- Column names are snake_case.
- Required fields are actually required.
- Indexes support expected queries.
- Foreign keys and delete behavior are intentional.
- No future-sprint schema is added.
- Migration was inspected before applying.

### AI Review Checklist

Check:

- Prompt is in a prompt file.
- Output schema is explicit.
- Parser expects structured JSON.
- Failure modes are explicit.
- Retry/timeout behavior is clear.
- Logs use structured properties.
- No agent framework introduced.
- No vector database introduced.

### Documentation Review Checklist

Update documentation when:

- Architecture changes.
- A decision is made or reversed.
- Sprint scope changes.
- A new module begins.
- A major implementation rule changes.
- A prohibited pattern changes.

Prefer updating existing docs over creating many small scattered docs.

---

## 10. Verification Before Completion

An AI agent must not claim a task is done only because code was written.

Before saying done, provide:

1. Summary of changed files
2. What changed
3. How it was tested
4. Commands run
5. Results of those commands
6. Risks or trade-offs
7. Follow-up tasks, if any
8. Suggested commit message

If checks were not run, say so clearly and explain why.

---

## 11. Git Workflow

Default workflow:

```txt
develop
→ feature branch
→ focused commits
→ PR
→ squash merge when appropriate
```

Branch naming examples:

```txt
feat/s2-01-resume-analysis-entity
feat/resume-upload-page
refactor/auth-form-components
fix/frontend-auth-check
polish/resume-flow-hardening
```

Commit message examples:

```txt
feat(backend): add resume analysis entity
feat(frontend): add resume list page
fix(frontend): handle auth check failures
refactor(frontend): extract auth form components
style(frontend): show pointer cursor on buttons
```

Rules:

- Do not commit unless explicitly asked.
- Keep commits focused.
- Do not include unrelated formatting changes.
- Do not commit secrets, local env files, uploads, build outputs, or generated artifacts unless intentionally required.
- Include migration files only when the ticket requires schema changes.

---

## 12. Multi-Agent Policy

Default policy:

```txt
One ticket
→ one branch
→ one AI agent
→ one clear workflow
```

Do not run multiple agents in parallel until:

- The repo has stable tests.
- Tickets are independent.
- File overlap is low.
- The developer can review all changes confidently.
- CI catches build and test failures reliably.

Safe parallel work examples in the future:

- Agent A: frontend-only page
- Agent B: backend-only tests
- Agent C: documentation update

Unsafe parallel work examples:

- Two agents editing the same module.
- One agent changing API contract while another builds frontend.
- One agent changing auth while another changes route protection.
- Multiple agents generating migrations.

For the current stage of the project, prefer single-agent execution with strict review.

---

## 13. Escalation Rules

Stop and ask for approval before:

- Changing architecture.
- Adding a new dependency.
- Adding a new module.
- Adding or changing a database migration.
- Changing auth/session behavior.
- Changing API response contracts.
- Introducing background jobs.
- Introducing queues, vector databases, microservices, or agent frameworks.
- Moving files across layers or modules.
- Refactoring unrelated code.

When blocked, do not guess. Explain:

- What is known
- What is unknown
- Options
- Recommended path
- Risk of each option

---

## 14. Standard Prompts

### Start a Ticket

```txt
Read CLAUDE.md, PROJECT_CONTEXT.md, docs/ARCHITECTURE.md, docs/DECISIONS.md, docs/ROADMAP.md, and docs/AI_WORKFLOW.md.

Classify this ticket as Track A or Track B.

Do not code yet.

First provide:
1. Ticket understanding
2. Scope boundary
3. Files to inspect
4. Expected files to change
5. Implementation plan
6. Risks/questions
```

### Review a Change

```txt
Review this change against docs/AI_WORKFLOW.md.

Focus on:
- sprint scope
- architecture boundaries
- backend/frontend conventions
- error handling
- test coverage
- over-engineering
- missing verification

Do not rewrite code unless necessary.
Return a prioritized review list.
```

### Finish a Ticket

```txt
Before claiming this ticket is done, summarize:
1. Changed files
2. What changed
3. Commands run
4. Test results
5. Manual test checklist
6. Risks/trade-offs
7. Suggested commit message
```

---

## 15. Anti-Patterns

Avoid:

- Coding before planning.
- Broad rewrites for narrow tickets.
- Adding abstractions before duplication is real.
- Creating generic components too early.
- Adding future modules early.
- Adding database tables before their sprint.
- Putting business logic in controllers.
- Using raw `fetch()` in components.
- Treating all auth failures as logged-out.
- Trusting stale TanStack Query data after a 401.
- Claiming completion without running checks.
- Using AI to hide uncertainty instead of surfacing it.

---

## 16. Definition of Done

A ticket is done only when:

- It satisfies the ticket acceptance criteria.
- It stays inside scope.
- It follows architecture rules.
- It passes relevant automated checks.
- It has been manually tested where appropriate.
- It has no known unmentioned risks.
- It is small enough to review.
- The final summary is clear enough for a PR description.
