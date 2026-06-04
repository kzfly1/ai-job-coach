# AI Job Coach
AI Job Coach is a full-stack MVP project designed to help early-career developers analyze resumes, compare job description, identify skill gaps, and prepare for interviews.

## Current Status
Sprint 1 is in progress.

Current setup includes:

- Local Git repository
- Docker Compose PostgreSQL setup
- Project documentation under `docs/`

Backend and frontend applications have not been scaffolded yet.

## Tech Stack

Planned stack:
- Backend: ASP.NET CORE (.NET 8)
- Frontend: Next.js, TypeScript, Tailwind CSS
- Database: PostgreSQL
- Local Development: Docker, Rider, DataGrip

## Local Database Setup

Start PostgreSQL:

```bash
docker compose up -d
```

Stop PostgresQL:

```bash
docker compose down
```

## Database Connection
For shared setup, copy .env.example to .env and update local values as needed

## Project Context
Architecture decision, sprint scope, and development constraints are documented in: 
`docs/PROJECT_CONTEXT.md`