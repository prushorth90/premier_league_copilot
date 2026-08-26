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
# Replace POSTGRES_PASSWORD in .env before first startup.
docker compose up --build
```

The containers expose:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5082`
- Health endpoint: `http://localhost:5082/health`
- Swagger UI: `http://localhost:5082/swagger`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`

Development Compose uses `frontend/Dockerfile.dev` and `backend/Dockerfile.dev`. The source folders are mounted into their containers; Vite hot module replacement and `dotnet watch` apply code changes while the stack is running. PostgreSQL and Redis ports are exposed for local inspection only.

Stop the stack while preserving database data:

```bash
docker compose down
```

Remove the containers and persistent PostgreSQL, Redis, and development cache volumes:

```bash
docker compose down --volumes
```

View service logs with `docker compose logs -f SERVICE`, where `SERVICE` is `frontend`, `backend`, `postgres`, or `redis`.

Check container and dependency health with:

```bash
docker compose ps
curl --fail http://localhost:5082/health
```

The health response is `Healthy` when PostgreSQL and Redis respond, `Degraded` when Redis is unavailable but the memory fallback is active, and `503 Unhealthy` when essential PostgreSQL storage is unavailable.

## Production deployment

The default Dockerfiles are multi-stage production builds. ASP.NET runs from the Alpine runtime image as the built-in non-root user, and React is served by unprivileged nginx. Application filesystems are read-only, Linux capabilities are dropped, and only the frontend port is published.

Create an untracked production environment file with strong random secrets:

```bash
cp .env.example .env.production
```

Set at minimum:

```dotenv
POSTGRES_PASSWORD=<strong-random-password>
REDIS_PASSWORD=<different-strong-random-password>
APP_ORIGIN=https://fpl.example.com
APP_PORT=8080
```

Start the hardened stack:

```bash
docker compose --env-file .env.production -f compose.production.yml up -d --build --wait
```

Open `http://localhost:8080` for a local production smoke test. In an internet deployment, terminate TLS at a trusted ingress or load balancer and forward traffic to the frontend container. Set `APP_ORIGIN` to the exact public HTTPS origin. Set `Security__UseHttpsRedirection=true` only when ASP.NET itself has a correctly configured HTTPS endpoint; it remains false behind the nginx/ingress HTTP hop.

Production nginx serves SPA routes, proxies `/api` and `/health` to ASP.NET, applies browser security headers, and caches fingerprinted assets. ASP.NET applies API security headers, request-size limits, per-client rate limiting, strict CORS, sanitized Problem Details, and structured request timing logs. Swagger is disabled outside Development.

Stop production while preserving data:

```bash
docker compose --env-file .env.production -f compose.production.yml down
```

Back up PostgreSQL and Redis volumes before upgrades. A logical PostgreSQL backup can be created with:

```bash
docker compose --env-file .env.production -f compose.production.yml \
	exec -T postgres pg_dump -U fpl -d fpl > fpl-backup.sql
```

Never commit `.env`, `.env.production`, database dumps, tokens, or real connection strings. The repository includes placeholders only; `.gitignore` excludes environment files and build artifacts.

## Continuous integration

The GitHub Actions CI workflow runs on every push and pull request. It performs:

- Frontend dependency installation, type-checking, linting, tests, and production build
- Backend NuGet restore, Release build, and all solution tests
- Docker Compose configuration validation and fresh frontend/backend image builds

The automated test suites use deterministic mocked FPL responses and do not require the public FPL API, Redis, or PostgreSQL unless a test explicitly targets an infrastructure integration. Coverage includes:

- Backend xUnit: transport deserialization and domain mapping, cache fallback and concurrent request coalescing, projected-point factors and horizons, captain ranking, all legal formation boundaries, transfer budgets and club limits, two-transfer combinations, recommendation ranking, persistence repositories, controllers, and middleware
- Frontend Vitest: setup validation and mocked team verification, routing, Dashboard data, PlayerCard states, pitch rows and bench order, player/fixture selectors, Transfers and Recommendations cards, recommendation horizon sorting, and loading/error states

CI runs every `*.test.ts`/`*.test.tsx` file through `npm test` and every xUnit test in `PremierLeagueCopilot.sln` through `dotnet test`; no per-file allowlist needs maintenance when new tests are added.

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

The frontend starts at `http://localhost:5173`. For standalone Vite development, set `VITE_API_BASE_URL=http://localhost:5082` in `frontend/.env`. Production uses same-origin `/api` requests through nginx and does not embed a backend host in the JavaScript bundle.

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

### Player headshots

Player DTOs derive `photoUrl` from the official Premier League static image service using the stable FPL player `code`: `https://resources.premierleague.com/premierleague/photos/players/110x140/p{code}.png`. The application does not scrape search engines or third-party image sites.

The React `PlayerHeadshot` component uses intrinsic `110x140` dimensions, fixed responsive containers, lazy loading, asynchronous decoding, and no-referrer requests. Missing player codes use the self-hosted `/images/player-placeholder.svg`; failed CDN requests switch to the same local placeholder. Production Content Security Policy allows images only from the application itself, data URLs, and `resources.premierleague.com`.

### AI Coach

The AI Coach uses the official `GitHub.Copilot.SDK` .NET package entirely inside the ASP.NET backend. The React application sends only the natural-language message and connected public FPL Team ID to `POST /api/coach/chat`; it never receives a GitHub token, SDK configuration, model credential, prompt context, or direct model endpoint.

```json
{
	"teamId": 7558250,
	"message": "Should I sell Saka?"
}
```

The backend validates the request, loads the public manager record, current-gameweek squad, and bootstrap player metadata through `IFplDataService`, and rejects an incomplete or duplicate squad. It passes the resulting typed 15-player context and the user's message into a fresh Copilot session with `FplCoachAgent` selected as the parent custom agent.

`FplCoachAgent` interprets the request and delegates factual investigation to the initial specialist agents:

- `InjurySpecialistAgent` calls `get_player_availability`, which returns only official FPL bootstrap availability for an owned player.
- `FixtureSpecialistAgent` calls `get_upcoming_fixtures`, which returns only official element-summary fixture data for an owned player.
- `TransferSpecialistAgent` calls `get_transfer_recommendations`, which returns only budget-, position-, and club-valid options from the existing transfer engine.

The parent can delegate but cannot call fact tools directly. Each specialist has an exclusive tool allowlist, ambiguous/non-owned player names fail closed, and no shell, file, web, or MCP tools are exposed. Agent prompts explicitly prohibit inventing injuries, fixtures, prices, budgets, or projected scores.

The strongly typed response includes the final message, recommendation type (`General`, `Availability`, `Transfer`, or `Replacement`), a 0-100 confidence score, and optional matched-player details:

```json
{
	"message": "Copilot-generated FPL guidance based on the supplied squad context...",
	"teamId": 7558250,
	"recommendationType": "Availability",
	"confidence": 78,
	"player": {
		"playerId": 12,
		"playerName": "Saka",
		"teamName": "Arsenal",
		"position": "MID",
		"status": "a",
		"chanceOfPlayingNextRound": null,
		"photoUrl": "https://resources.premierleague.com/premierleague/photos/players/110x140/p223340.png"
	},
	"isMocked": false
}
```

The frontend maintains the current conversation in memory and includes pending, failure, and retry states. Messages are limited to 1,000 characters; invalid requests return `400 Bad Request`, missing FPL teams return `404 Not Found`, and Copilot SDK failures return sanitized `502 Bad Gateway` Problem Details. `ICoachService` owns FPL context assembly, `IFplCoachFactService` owns read-only fact retrieval, and `ICopilotChatClient` isolates all SDK sessions and custom-agent configuration.

The SDK can use its bundled runtime with `COPILOT_GITHUB_TOKEN`, or connect to a private external headless runtime using `COPILOT_RUNTIME_URL` and an optional connection token. The GitHub account or organization must permit Copilot SDK/CLI features. A valid token alone is insufficient when enterprise or organization policy disables SDK access.

Set `VITE_API_BASE_URL` only for standalone development when the API is on another origin.

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

The backend requires a valid `ConnectionStrings__PostgreSQL` value. The checked-in `appsettings.json` contains no password; supply credentials through an ignored environment file, secret manager, or deployment platform.

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

All FPL cache keys and expirations are defined by `IFplCachePolicyProvider`. Keys are namespaced and schema-versioned so contract changes can invalidate only the affected resource:

| Resource | Default expiration | Relative volatility |
| --- | ---: | --- |
| Bootstrap player/team catalogue | 60 minutes | Long-lived reference and player catalogue data |
| Player fixture/history summary | 30 minutes | Player-specific projections and recent history |
| Fixtures | 15 minutes | Scores, kickoff state, and gameweek scheduling |
| Manager summary | 5 minutes | Frequently changing entry totals and bank |
| Manager gameweek picks | 5 minutes | Frequently changing squad and gameweek state |

The cache coordinator uses an in-process memory cache as L1 and Redis as L2. On a miss, concurrent requests for the same key share one upstream FPL task, preventing Dashboard, Transfers, and Recommendations queries from causing duplicate public API calls. Caller cancellation stops only that caller from waiting and does not cancel shared work needed by other requests.

Redis reads, writes, connection failures, and malformed cached JSON fail open. Successful upstream responses remain in the local memory fallback for the same policy duration, so a temporary Redis outage does not make the application unavailable or repeatedly hit the FPL API.

### FPL REST API

The React frontend uses the backend endpoints below and never communicates with the public FPL API directly. In these routes, `teamId` is the public FPL entry ID.

On first use, the frontend asks for this public team ID, verifies it through the backend, and stores it in browser local storage. The Settings page can verify a replacement ID or remove the saved ID to restart setup.

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/fpl/team/{teamId}` | Manager and fantasy team summary |
| `GET` | `/api/fpl/team/{teamId}/squad` | Current gameweek squad with enriched player details |
| `GET` | `/api/fpl/players` | All players with team, position, price, availability, and points |
| `GET` | `/api/fpl/fixtures` | Fixtures with team names, scores, kickoff, and difficulty |
| `POST` | `/api/coach/chat` | Mocked team-aware natural-language coach response |
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

Team IDs must be positive integers. Invalid inputs return `400 Bad Request`, missing public FPL entries return `404 Not Found`, throttled clients return `429 Too Many Requests`, unavailable upstream FPL data returns `502 Bad Gateway`, and unavailable essential storage returns `503 Service Unavailable`. All errors use sanitized Problem Details JSON with a trace ID. Interactive schemas and response contracts are available in Development Swagger at `http://localhost:5082/swagger`.

## Environment variables

Example values live in `.env.example`, `frontend/.env.example`, and `backend/.env.example`. Local `.env` files are ignored by Git.

| Variable | Required | Purpose |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | Production | PostgreSQL password; no production default |
| `REDIS_PASSWORD` | Production | Redis password; no production default |
| `POSTGRES_DB` / `POSTGRES_USER` | No | Database name/user, both default to `fpl` |
| `APP_ORIGIN` | Production | Exact browser origin allowed by CORS |
| `APP_PORT` | No | Published production frontend port, default `8080` |
| `ConnectionStrings__PostgreSQL` | Backend | Full EF Core PostgreSQL connection string |
| `Redis__ConnectionString` | Backend | StackExchange.Redis connection string |
| `Cors__AllowedOrigins__0` | Backend | First exact allowed browser origin |
| `Persistence__ApplyMigrations` | No | Apply pending EF migrations at startup |
| `Persistence__RecommendationSnapshotMinutes` | No | PostgreSQL recommendation snapshot lifetime |
| `Security__RequestLimitPerMinute` | No | Per-client API request limit, default `120` |
| `Security__MaxRequestBodyKilobytes` | No | Kestrel request-body limit, default `64` |
| `Security__UseHttpsRedirection` | No | Enable only when ASP.NET owns HTTPS |
| `VITE_API_BASE_URL` | Development only | Cross-origin backend base URL for standalone Vite |
| `COPILOT_MODEL` | No | Copilot model selection, default `auto` |
| `COPILOT_GITHUB_TOKEN` | Bundled runtime | Backend-only GitHub token for Copilot SDK authentication |
| `COPILOT_RUNTIME_URL` | External runtime | Private Copilot CLI headless server URL |
| `COPILOT_RUNTIME_CONNECTION_TOKEN` | No | Shared secret for an external runtime connection |
| `COPILOT_REQUEST_TIMEOUT_SECONDS` | No | Copilot request timeout, default `120` |

## Troubleshooting

- `503` from `/health`: inspect `dependencies.postgresql`; verify the connection string and `docker compose logs postgres backend`.
- `Degraded` from `/health`: Redis is unavailable. Requests continue through the memory fallback; inspect `docker compose logs redis backend`.
- Browser CORS failure: ensure `APP_ORIGIN` or `Cors__AllowedOrigins__0` exactly matches scheme, host, and port. Paths and wildcard origins are rejected.
- Migration failure: run `dotnet tool restore`, generate an idempotent script, and verify PostgreSQL credentials before restarting.
- AI Coach returns `502`: verify backend-only token/runtime configuration and confirm the GitHub organization or enterprise policy enables Copilot SDK/CLI access. Provider details remain in backend logs only.
- Stale development dependencies: run `docker compose down --volumes` only when discarding local PostgreSQL/Redis data is acceptable, then rebuild.