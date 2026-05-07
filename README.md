# Transaction Aggregation API

An enterprise-grade, Clean Architecture .NET 8 Web API for ingesting, categorizing, and aggregating financial transactions.

## Architecture

The solution follows Clean Architecture and Domain-Driven Design (DDD) principles:

- **Domain**: Contains enterprise logic, core entities (`Transaction`, `Category`), enums, and domain exceptions. Business invariants are enforced within aggregate roots.
- **Application**: Contains use cases (CQRS via MediatR), DTOs, and validation logic (FluentValidation).
- **Infrastructure**: Contains EF Core data access, Redis caching, third-party API mock integrations, and background services.
- **Api**: The presentation layer containing Controllers, JWT Authentication, Global Exception Handling, and Serilog configuration.

## Key Features

1. **Idempotent Data Ingestion**:
   - Ingests from multiple sources (mock REST API, CSV File).
   - Enforces unique `ExternalId` to prevent duplicate transaction entries.
   - Background service periodically triggers ingestion.
2. **Rule-Based Categorization**:
   - Automatically assigns categories based on `MerchantName` matching predefined keywords.
   - Categorization engine uses in-memory caching to avoid database lookups per transaction.
3. **High-Performance Aggregations**:
   - Aggregation endpoints (Total Spend, Monthly Summary, Top Merchants) utilize **Redis** for distributed caching.
4. **Resilience & Observability**:
   - **Serilog** for structured logging to console and files.
   - Global exception middleware prevents stack trace leakage and maps domain exceptions to HTTP status codes.
   - Rate limiting middleware enabled.
5. **Security**:
   - JWT-based Authentication.
   - Role-Based Access Control (`Admin` vs `User`).

## Tech Stack

- **Framework**: .NET 8.0
- **Database**: SQL Server 2022 (Entity Framework Core 8)
- **Caching**: Redis (StackExchange.Redis)
- **Resilience**: Polly v8 (Microsoft.Extensions.Http.Resilience)
- **Patterns**: CQRS (MediatR), Repository, Unit of Work, Strategy
- **Libraries**: AutoMapper, FluentValidation, CsvHelper
- **Orchestration**: Docker Compose

## Architectural Decisions & Trade-offs

1. **Modular Monolith over Microservices**: 
   - A single cohesive boundary minimizes deployment complexity while Clean Architecture ensures distinct separation of concerns. This allows for an easy transition to microservices later if scaling demands it.
2. **SQL Server over NoSQL**:
   - Financial transactions inherently demand strict ACID compliance, structured schemas, and robust relational querying (e.g. grouped aggregations).
3. **Pushing Aggregations down to SQL**:
   - Complex reports (Total Spend, Monthly Summary) are evaluated at the database level (`GROUP BY`) instead of loading entities into memory, guaranteeing efficient processing.
4. **Resilience with Polly**:
   - External dependencies (APIs, IO streams) are wrapped in a `ResiliencePipeline` with an exponential backoff strategy.
5. **Decoupled Categorization Engine**:
   - Following the **Strategy Pattern**, categorization logic is abstracted behind `ICategorizationStrategy`. The current `DatabaseRuleCategorizationStrategy` can easily be swapped or augmented with an ML-based strategy.
6. **Smart Caching**:
   - Redis selectively caches high-cost query results. Cache invalidation happens immediately via key prefixing (`RemoveByPrefixAsync("aggregations:")`) upon successful new data ingestion.

## Getting Started

### Prerequisites

- Docker Desktop
- .NET 8 SDK (for local development)

### Running with Docker Compose (Recommended)

1. Clone the repository.
2. Navigate to the root directory `c:\dev\Capitec`.
3. Create a `.env.docker` file (you can copy the contents from the provided `.env.docker` template).
4. Run Docker Compose:

```bash
docker-compose up --build -d
```

This will spin up:
- SQL Server on port `1433`
- Redis on port `6379`
- API on port `8080`

The database will be automatically seeded with categories and categorization rules on startup.

### API Documentation (Swagger)

Once running locally, navigate to:
`http://localhost:8080/` (or `http://localhost:5000/` if running outside Docker).

### Authentication

To test secured endpoints, first get a token from the `/api/Auth/token` endpoint:

**Admin User:**
- Email: `admin@example.com`
- Password: `Admin@123!`

**Standard User:**
- Email: `user@example.com`
- Password: `User@123!`

Click "Authorize" in Swagger and paste the token as: `Bearer <your-token>`.

## Testing

The solution includes a comprehensive unit test suite using `xUnit`, `Moq`, and `FluentAssertions`.

```bash
dotnet test
```
