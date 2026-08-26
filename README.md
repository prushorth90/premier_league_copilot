# Fantasy Premier League Recommendation App

Initial full-stack foundation for an FPL recommendation application. This repository currently contains project infrastructure only; no player, squad, or recommendation features are implemented yet.

## Stack

- Frontend: React 19, TypeScript, Vite 8, React Router, and Tailwind CSS 4
- Backend: C# and ASP.NET Core Web API on .NET 10
- API documentation: Swagger UI and OpenAPI
- Infrastructure: PostgreSQL, Redis, and Docker Compose

## Project structure

```text
.
├── frontend/   React application
└── backend/    ASP.NET Core Web API
```

## Prerequisites

- Node.js 22 or later
- npm 10 or later
- .NET SDK 10.0 or later
- Docker Desktop for the containerized workflow

## Docker development

Start the complete development stack from the repository root:

```bash
cp .env.example .env
docker compose up --build
```

The containers expose:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5082`
- Health endpoint: `http://localhost:5082/health`
- Swagger UI: `http://localhost:5082/swagger`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`

The frontend and backend source folders are mounted into their containers. Vite hot module replacement and `dotnet watch` apply code changes while the stack is running.

Stop the stack while preserving database data:

```bash
docker compose down
```

Remove the containers and persistent PostgreSQL, Redis, and development cache volumes:

```bash
docker compose down --volumes
```

View service logs with `docker compose logs -f SERVICE`, where `SERVICE` is `frontend`, `backend`, `postgres`, or `redis`.

## Frontend

```bash
cd frontend
cp .env.example .env
npm install
npm run dev
```

The frontend starts at `http://localhost:5173`. It can run without the backend.

Useful checks:

```bash
npm run build
npm run lint
```

## Backend

```bash
cd backend
cp .env.example .env
set -a && source .env && set +a
dotnet restore
dotnet run
```

The backend starts at `http://localhost:5082`. It can run without the frontend.

- Health endpoint: `http://localhost:5082/health`
- Swagger UI: `http://localhost:5082/swagger`
- OpenAPI document: `http://localhost:5082/swagger/v1/swagger.json`

To build without starting the server:

```bash
dotnet build
```

Swagger is enabled when `ASPNETCORE_ENVIRONMENT` is set to `Development`.

## Environment variables

Example values live in `frontend/.env.example` and `backend/.env.example`. Local `.env` files are ignored by Git.