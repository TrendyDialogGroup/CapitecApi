# Transaction Aggregation API

An enterprise-grade, Clean Architecture .NET 8 Web API for ingesting, categorizing, and aggregating financial transactions.

## Architecture

The solution follows Clean Architecture and Domain-Driven Design (DDD) principles:

- **Domain**: Contains enterprise logic, core entities (`Transaction`, `Category`, `CategorizationRule`), enums, and domain exceptions. Business invariants are enforced within aggregate roots.
- **Application**: Contains use cases (CQRS via MediatR), DTOs, FluentValidation, and pipeline behaviours.
- **Infrastructure**: Contains EF Core data access, Redis caching, mock REST/CSV ingestion, Polly resilience, JWT issuance, and background services.
- **Api**: Controllers, JWT authentication/authorization, global exception handling, rate limiting, health checks, and Serilog.

Dependency rule: **Api → Application → Domain**; **Infrastructure → Application + Domain**. Domain never depends outward.

## Key Features

1. **Idempotent Data Ingestion**
   - Ingests from multiple sources (mock REST API, CSV file).
   - Batch duplicate check via `GetExistingExternalIdsAsync` + unique DB index on `ExternalId`.
   - Admin `POST /api/v1/Transactions/ingest` returns **202** and runs via a bounded Channel-backed background queue.
   - Optional periodic `TransactionIngestionJob` (configurable interval).
2. **Rule-Based Categorization**
   - Strategy pattern (`ICategorizationStrategy`) with `DatabaseRuleCategorizationStrategy`.
   - Keyword rules cached in memory (10 minutes) to avoid per-transaction DB hits.
3. **High-Performance Aggregations**
   - Total spend / monthly summary / top merchants computed with SQL `GROUP BY` where possible.
   - Redis caches results; ingest invalidates `aggregations:*`.
4. **Resilience & Observability**
   - Serilog structured logging + correlation ID middleware (`X-Correlation-ID`).
   - Global exception middleware maps domain exceptions to HTTP status codes without leaking internals.
   - AspNetCoreRateLimit (100/min, 1000/hour).
5. **Security**
   - JWT authentication with RBAC policies: `AdminOnly`, `UserOrAdmin`.
   - Demo token endpoint (replace with a real IdP in production).

## Tech Stack

| Area | Choice |
|------|--------|
| Runtime | .NET 8 |
| API | ASP.NET Core, Swashbuckle |
| CQRS / validation / mapping | MediatR 12, FluentValidation, AutoMapper 15 |
| Data | EF Core 8 + SQL Server 2022 |
| Cache | Redis (StackExchange.Redis) |
| Resilience | Polly v8 resilience pipelines |
| CSV | CsvHelper |
| Tests | xUnit, Moq, FluentAssertions, WebApplicationFactory |
| Ops | Docker Compose (SQL + Redis + API) |

## Architectural Decisions & Trade-offs

These are the decisions a review panel will typically probe. Understand the *why*, not just the *what*.

### 1. Modular monolith over microservices
A single deployable with Clean Architecture boundaries keeps operational cost low for an assessment/demo while still allowing a future split (e.g. Ingestion vs Aggregations) because Domain/Application ports already isolate concerns.

**Trade-off:** You don't get independent scaling today; you do get simpler transactions, debugging, and Docker Compose.

### 2. Clean Architecture + CQRS (MediatR)
Controllers stay thin; each use case is a command/query handler. Pipeline behaviours cross-cut logging and validation without polluting handlers.

**Trade-off:** More files/ceremony than a simple service layer; worth it for testability and clear request flow.

### 3. SQL Server over NoSQL
Financial data needs ACID writes, relational integrity (`ExternalId` uniqueness), and efficient `GROUP BY` aggregations.

**Trade-off:** Harder horizontal write scaling than a document store; acceptable for this domain.

### 4. Aggregations pushed to SQL + Redis cache
Reports run as database projections (`GROUP BY` / date-range filters). Redis caches the DTO results. Cache keys include filter dimensions (dates, `take`). Successful ingest deletes keys with prefix `aggregations:`.

**Trade-off:** Prefix delete uses Redis `SCAN` (safe, not free). Cache stampede protection is basic (get-or-set); production might add locking or stampede mitigation.

### 5. Dual-layer idempotency
1. Application: batch-load existing external IDs into a `HashSet` (avoids N+1).
2. Database: unique index on `ExternalId` as the final safety net under concurrency.

**Trade-off:** Unique index violations still need handling under extreme races; current happy path relies on the batch check + single writer queue.

### 6. Strategy pattern for categorization
`ICategorizationStrategy` lets us add ML / merchant-master strategies later without rewriting ingestion. Current strategy is keyword + priority with in-memory cache.

**Trade-off:** Keyword matching is brittle (`Contains`); good for demo, not for production merchant resolution.

### 7. Polly around external sources
CSV/REST fetch paths execute through a named resilience pipeline (exponential backoff retries) so transient IO/network faults don't fail the whole ingest run.

**Trade-off:** REST source is currently a **mock** that simulates failures to exercise Polly; `IHttpClientFactory` is wired for the real client swap.

### 8. Channel-based background queue for ingest
`POST /ingest` accepts work quickly (202) and processes via `QueuedHostedService`. Avoids tying HTTP latency to slow multi-source ingestion.

**Trade-off:** In-process queue is lost on process crash; Hangfire/Azure Queue would be the production upgrade.

### 9. JWT demo auth (not a real IdP)
Enough to demonstrate RBAC for the assessment. Production would use Entra ID / Auth0 / IdentityServer and never hard-code passwords.

### 10. Testing strategy
- **Unit:** Domain invariants, handlers (with mocked ports), categorization, validators, middleware, CSV parsing, JWT claims.
- **Integration:** `WebApplicationFactory` with EF InMemory + in-memory cache double, covering auth, RBAC, ingest→list, aggregations, health.

## Getting Started

### Prerequisites

| Path | Need |
|------|------|
| Docker (full stack) | Docker Desktop |
| Local API | .NET 8 SDK + Docker Desktop (for SQL Server + Redis only) |
| Tests only | .NET 8 SDK |

### Option A — Full stack with Docker (recommended)

From the repo root:

```bash
docker compose --env-file .env.docker up --build -d
```

Wait until healthy, then open:
- Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health

This starts:
- SQL Server → `localhost:1433`
- Redis → `localhost:6379`
- API → `localhost:8080`

Categories/rules are seeded automatically on API startup. Sample CSV from `data/imports` is mounted into the container.

Useful commands:

```bash
docker compose --env-file .env.docker ps
docker compose --env-file .env.docker logs -f api
docker compose --env-file .env.docker down
```

**Disk note:** SQL Server + .NET SDK/runtime images need several GB. If `docker compose up` hangs or the API stays in `Created`, free disk space, then run:

```bash
docker builder prune -af
docker compose --env-file .env.docker down
docker compose --env-file .env.docker up --build -d
```

### Option B — Local API (`dotnet run`) + Docker infra

1. Start only SQL Server and Redis:

```bash
docker compose -f docker-compose.infra.yml --env-file .env.docker up -d
```

2. Wait ~30–60s for SQL Server to become healthy, then run the API:

```bash
dotnet restore
dotnet run --project src/TransactionAggregator.Api
```

3. Open Swagger: http://localhost:5042/swagger  
   (HTTPS profile: https://localhost:7026/swagger)

`appsettings.json` already points at `localhost:1433` and `localhost:6379`.

### Authentication

`POST /api/v1/Auth/token`

**Admin**
- Email: `admin@example.com`
- Password: `Admin@123!`

**User**
- Email: `user@example.com`
- Password: `User@123!`

In Swagger → Authorize → `Bearer <token>`.

### Main endpoints

| Method | Route | Policy |
|--------|-------|--------|
| POST | `/api/v1/Auth/token` | Anonymous |
| GET | `/api/v1/Transactions` | UserOrAdmin |
| POST | `/api/v1/Transactions/ingest` | AdminOnly |
| GET | `/api/v1/Aggregations/total-spend-by-category` | UserOrAdmin |
| GET | `/api/v1/Aggregations/monthly-summary` | UserOrAdmin |
| GET | `/api/v1/Aggregations/top-merchants` | UserOrAdmin |
| GET | `/health` | Public |

## Testing

No Docker required for tests (EF InMemory + cache doubles):

```bash
dotnet test
```

Unit + integration suites use xUnit, Moq, FluentAssertions, and ASP.NET Core `WebApplicationFactory`.

## Honest follow-ups (good interview answers)

If asked what you would harden next:

1. Replace demo auth with a real identity provider.
2. Replace mock REST with a real typed `HttpClient` + contract tests.
3. Dispatch or remove unused domain events (`TransactionCreatedEvent`).
4. Move the ingest queue to a durable outbox/queue.
5. Add category/rule admin APIs instead of seed-only configuration.
6. Tighten Redis cache stampede behaviour and monitoring.
