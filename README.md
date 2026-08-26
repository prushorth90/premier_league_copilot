# Fantasy Premier League Recommendation App

Full-stack FPL analysis application with live team, squad, player, fixture, and deterministic projected-points data.

## Stack

- Frontend: React 19, TypeScript, Vite 8, React Router, and Tailwind CSS 4
- Backend: C# and ASP.NET Core Web API on .NET 10
- API documentation: Swagger UI and OpenAPI
- Infrastructure: PostgreSQL, Redis, and Docker Compose

## Project structure

```text
.
├── frontend/       React application
├── backend/        ASP.NET Core Web API
└── backend.Tests/  Backend unit tests
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

## Continuous integration

The GitHub Actions CI workflow runs on every push and pull request. It performs:

- Frontend dependency installation, type-checking, linting, tests, and production build
- Backend NuGet restore, Release build, and all solution tests
- Docker Compose configuration validation and fresh frontend/backend image builds

Run the same application checks locally with:

```bash
cd frontend
npm ci
npm run typecheck
npm run lint
npm test
npm run build

cd ../
dotnet restore PremierLeagueCopilot.sln
dotnet build PremierLeagueCopilot.sln --configuration Release --no-restore
dotnet test PremierLeagueCopilot.sln --configuration Release --no-build

docker compose build
```

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
npm run typecheck
npm run build
npm run lint
npm test
```

### Frontend data layer

The frontend uses a typed API client and TanStack Query for all backend FPL requests. Team and squad data remain fresh for 5 minutes, fixtures for 15 minutes, and players for 60 minutes. Queries refetch when stale on window focus, retry transient failures, and skip retries for missing resources. The Dashboard also provides a manual refresh action for all four resources.

The Transfers page shows live bank and free-transfer availability, then ranks the top single moves and jointly funded two-transfer combinations. Users can switch between 1-gameweek and 5-gameweek ordering while retaining visible 1/3/5-gameweek gains, incoming fixtures, confidence, price impact, and recommendation reasoning.

The Recommendations page combines captaincy, the legal starting XI and bench order, projected XI totals over 1/3/5 gameweeks, the best single and two-transfer moves, and recurring sale candidates into one decision dashboard. Factor explanations remain visible, and transfer confidence is shown as high confidence at 80% or above and speculative below that threshold.

Set `VITE_API_BASE_URL` in `frontend/.env` when the API is not running at `http://localhost:5082`.

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

### Backend structure

- `Controllers`: HTTP routing and response handling
- `Services`: application service contracts and implementations
- `Models`: internal domain and application models
- `DTOs`: public API request and response contracts
- `ExternalClients`: configured outbound HTTP clients
- `Configuration`: strongly typed application options
- `Middleware`: centralized exception handling
- `Recommendation`: deterministic projection factors and recommendation composition

The backend validates FPL API, PostgreSQL, and Redis configuration during startup. Controllers and services use async methods and propagate request cancellation tokens.

### PostgreSQL persistence

Entity Framework Core with the Npgsql provider stores only application-owned data:

- Local profiles and their selected public FPL team ID
- Per-profile application settings as typed JSON values
- One expiring current recommendation snapshot per FPL team and recommendation kind
- Append-only recommendation history for freshly calculated responses

Public FPL players, teams, fixtures, picks, and history are not duplicated in PostgreSQL. Those resources continue to come from the typed FPL client and Redis cache.

The backend applies pending migrations at startup when `Persistence__ApplyMigrations=true`. The snapshot lifetime is configured with `Persistence__RecommendationSnapshotMinutes`. Docker Compose supplies the PostgreSQL connection through `ConnectionStrings__PostgreSQL` using the root `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD` environment values.

Restore the repository-local EF tool and manage migrations from the repository root:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add MigrationName \
	--project backend/backend.csproj \
	--startup-project backend/backend.csproj \
	--output-dir Persistence/Migrations
dotnet tool run dotnet-ef database update \
	--project backend/backend.csproj \
	--startup-project backend/backend.csproj
```

`IProfileRepository` owns profile and setting persistence. `IRecommendationStore` owns fresh snapshot reads, snapshot replacement, and recommendation-history appends, keeping EF concerns out of recommendation algorithms and controllers.

### Projected points

`IProjectedPointsService.GetPlayerProjectionAsync` estimates a requested player's points over the next 1, 3, and 5 distinct gameweeks. Double-gameweek fixtures are included in the same horizon gameweek.

The initial deterministic formula is additive and runs these isolated factors in order:

- Position-specific baseline
- Recent FPL form
- Expected minutes from the five latest appearances
- Fixture difficulty
- Home or away venue
- Historical FPL points per 90 from the latest three seasons
- Availability multiplier from player status and chance of playing

Scores are floored at zero and rounded to two decimal places. Results include fixture-level and horizon-level factor contributions with plain-language explanations, plus an explicit rounding adjustment when required so every breakdown reconciles exactly to its projected score.

### Captain recommendations

The captain recommendation service ranks only the user's starting XI for the current gameweek. It combines projected points, expected minutes, fixture quality, position-adjusted expected goals and assists, and availability into a deterministic score. The response includes the best captain, vice captain, three alternatives, and every factor's score and plain-language explanation.

### Lineup recommendations

The lineup recommendation service evaluates the user's existing 15-player squad across every legal FPL formation. Each player is ranked using 80% of projected points plus an expected-minutes contribution worth up to 2 ranking points. The selected XI always contains exactly one goalkeeper, 3-5 defenders, 2-5 midfielders, and 1-3 forwards.

The response includes the formation, ordered starting XI, and bench order. Outfield substitutes are ordered by rank and the reserve goalkeeper occupies the fourth bench slot. Lineup changes identify players moving into the XI and players moving to the bench compared with the user's current selection.

### Transfer recommendations

The transfer recommendation service evaluates same-position replacements for every player in the existing 15-player squad. Candidates must be affordable using the player's FPL selling price plus bank, must not already be owned, must be available or doubtful, must have at least 30 expected minutes, and must leave the squad with no more than three players from any Premier League club. Same-position replacement preserves the required 2 goalkeepers, 5 defenders, 5 midfielders, and 3 forwards.

Recommendations compare projected points over the next 1, 3, and 5 gameweeks. Ranking normalizes the cumulative 3- and 5-gameweek gains to a per-gameweek rate and weights the horizons 50%, 30%, and 20%. Each result includes player out/in details, price difference, horizon gains, weighted gain, a 0-100 confidence score, and explanations for expected points, fixture quality, expected minutes, availability, and budget.

The same endpoint also returns two-transfer combinations ranked over the 3- and 5-gameweek horizons at 60% and 40%. Combination affordability is evaluated after adding bank and both exact selling prices, so savings from one move can fund the other. Every resulting squad is checked for distinct players, unchanged position allocation, and the three-player club limit.

To keep combination search interactive, the engine retains each outgoing player's strongest projected replacements plus its cheapest budget-enabling options, then evaluates and deduplicates only pairs from that bounded pool. The `limit` query parameter controls both the number of single transfers and the number of combinations returned rather than exposing every valid possibility.

### FPL data client

`IFplDataService` provides typed, asynchronous access to these public FPL resources:

- Bootstrap data from `bootstrap-static/`
- Fixtures from `fixtures/`
- Manager details from `entry/{managerId}/`
- Manager squad picks from `entry/{managerId}/event/{gameweek}/picks/`
- Player fixtures and history from `element-summary/{playerId}/`

Transport DTOs are mapped to application models before data leaves the service. Redis caches bootstrap data for 60 minutes, fixtures for 15 minutes, manager and squad data for 5 minutes, and player history for 30 minutes by default. Cache failures fall back to the upstream API, while upstream failures are logged and returned through centralized exception handling as `502 Bad Gateway` responses.

### FPL REST API

The React frontend uses the backend endpoints below and never communicates with the public FPL API directly. In these routes, `teamId` is the public FPL entry ID.

On first use, the frontend asks for this public team ID, verifies it through the backend, and stores it in browser local storage. The Settings page can verify a replacement ID or remove the saved ID to restart setup.

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/fpl/team/{teamId}` | Manager and fantasy team summary |
| `GET` | `/api/fpl/team/{teamId}/squad` | Current gameweek squad with enriched player details |
| `GET` | `/api/fpl/players` | All players with team, position, price, availability, and points |
| `GET` | `/api/fpl/fixtures` | Fixtures with team names, scores, kickoff, and difficulty |
| `GET` | `/api/recommendations/{teamId}/captain` | Ranked captain, vice captain, alternatives, and factor explanations |
| `GET` | `/api/recommendations/{teamId}/lineup` | Best legal starting XI, formation, bench order, and current-lineup changes |
| `GET` | `/api/recommendations/{teamId}/transfers?limit=20` | Valid transfer upgrades ranked across 1, 3, and 5 gameweeks |
| `GET` | `/api/recommendations/{teamId}/history?kind=captain&limit=20` | Persisted recommendation history, optionally filtered by kind |
| `GET` | `/api/profiles` | List local application profiles |
| `POST` | `/api/profiles` | Create a local profile with an optional selected FPL team ID |
| `GET` | `/api/profiles/{profileId}` | Read a local profile |
| `PUT` | `/api/profiles/{profileId}/team` | Update or clear the selected FPL team ID |
| `GET` | `/api/profiles/{profileId}/settings/{key}` | Read a typed JSON application setting |
| `PUT` | `/api/profiles/{profileId}/settings/{key}` | Create or replace a typed JSON application setting |

Team IDs must be positive integers. Invalid IDs return `400 Bad Request`, missing public FPL entries return `404 Not Found`, and unavailable upstream data returns `502 Bad Gateway`. All errors use Problem Details JSON. Interactive schemas and response contracts are available in Swagger at `http://localhost:5082/swagger`.

## Environment variables

Example values live in `.env.example`, `frontend/.env.example`, and `backend/.env.example`. Local `.env` files are ignored by Git.