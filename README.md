# AI Job Coach

AI Job Coach helps software engineers get interviews and offers — not by optimising ATS
scores, but by making applications better and learning from what happens after them.

The product has two tracks:

- **Application Copilot** — upload a resume and a job description, get a structured
  profile of each, a match report with concrete skill gaps, and tailored resume content.
- **Career Intelligence** — track applications, journal interviews, and surface the
  patterns behind rejections over time.

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 8), EF Core 8, Serilog, FluentValidation |
| Frontend | Next.js (App Router), TypeScript, Tailwind CSS, shadcn/ui, TanStack Query |
| Database | PostgreSQL 16 |
| AI | OpenAI API over direct HTTP |
| Auth | Custom JWT in an HttpOnly cookie, BCrypt password hashing |

## Prerequisites

.NET 8 SDK · Node.js 20+ · Docker Desktop

## Local Setup

**1. Database**

```bash
cp .env.example .env      # adjust values as needed
docker compose up -d
```

Postgres listens on the port set by `POSTGRES_PORT` (`5434` by default, mapped to `5432`
in the container). Stop it with `docker compose down`.

**2. Backend configuration**

The committed `appsettings.json` holds placeholders, not working values. Two settings are
required before the API will start — supply them from the environment so nothing secret is
written to a tracked file:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5434;Database=aijobcoach;Username=aijobcoach_user;Password=change-me"
export Jwt__Secret="a-local-development-secret-at-least-32-chars"
```

Use the values you set in `.env`. `Jwt__Secret` is validated at startup and must be at
least 32 characters — the app refuses to start otherwise.

`OpenAI__ApiKey` is optional in Development and required outside it. AI features fail
without it; the rest of the app runs.

On PowerShell, use `$env:Jwt__Secret = "..."` instead of `export`.

**3. Backend**

```bash
dotnet ef database update --project src/AIJobCoach.Api
dotnet run --project src/AIJobCoach.Api
```

The API listens on `http://localhost:5001` and `https://localhost:7001`.

**4. Frontend**

```bash
cd frontend
npm install
npm run dev
```

The app runs at `http://localhost:3000`, which is the origin the API allows in
Development. The frontend defaults to the API at `http://localhost:5001`; override it
with `NEXT_PUBLIC_API_BASE_URL` if you change the backend port.

Use HTTP on both sides locally. Mixing HTTP and HTTPS across the two breaks the auth
cookie.

## Tests

```bash
dotnet test
cd frontend && npm run lint
```

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — system shape, module and layer
  conventions, integration seams, production requirements.
- [docs/DECISIONS.md](docs/DECISIONS.md) — architectural decisions, their trade-offs, and
  what would justify revisiting them.
- [CLAUDE.md](CLAUDE.md) — rules and boundaries for AI coding agents working in this
  repository.

Current implementation state is not tracked in documentation — read the code.
