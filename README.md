# TicketFlow

TicketFlow is a production-oriented .NET Web API for event ticket booking.

The project began as a working ASP.NET Core API and has evolved through staged architecture phases into a modular monolith with production-oriented infrastructure, messaging, caching, observability, containerization, and automated integration testing.

## Current Status

**Phase 4 – Product Feature Expansion (In Progress)**

The solution is organized into:

- `TicketFlow.Domain`
- `TicketFlow.Application`
- `TicketFlow.Contracts`
- `TicketFlow.Infrastructure`
- `TicketFlow.Presentation`
- `TicketFlow.Tests`

The API currently supports authentication, role-based authorization, events, seats, bookings, JWT access and refresh tokens, validation, structured logging, API versioning, Swagger/OpenAPI, concurrency protection, background processing, durable messaging, caching, observability, health checks, and automated integration testing.

The current development focus is expanding the product functionality while reusing the production infrastructure introduced during Phase 3.

## Architecture

TicketFlow follows a modular monolith / Clean Architecture-style structure:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
Presentation
```

`Contracts` contains shared API and integration-event contracts where appropriate.

Application logic uses CQRS with MediatR. Infrastructure concerns such as EF Core, ASP.NET Core Identity, Redis, RabbitMQ/MassTransit, background processing, and external integrations remain behind Application abstractions.

## Production Backend Capabilities

Phase 3 introduced the production-oriented infrastructure used by the application:

- JWT access and refresh-token lifecycle
- Role-based authorization
- CQRS with MediatR
- Domain and Application Events
- Transactional Outbox Pattern
- RabbitMQ messaging with MassTransit
- Background processing
- Redis cache-aside caching
- Bulk seat creation with database uniqueness guarantees
- OpenTelemetry distributed tracing and metrics
- Liveness and readiness health checks
- Docker and Docker Compose
- .NET Aspire for local development orchestration
- SQL Server and Redis integration testing with Testcontainers

Resource/ownership authorization is intentionally being introduced alongside booking update and cancellation functionality, where ownership rules become part of the business use case.

## Messaging and Reliability

TicketFlow uses the Outbox Pattern to coordinate database changes with integration-event publication.

```text
HTTP Request
    ↓
Application Command
    ↓
SQL Transaction
    ├── Domain State
    └── Outbox Message
    ↓
Background Outbox Processor
    ↓
MassTransit
    ↓
RabbitMQ
    ↓
Consumer
```

This provides at-least-once message delivery semantics while preventing database state from being committed without the corresponding integration event being durably recorded.

Consumers are designed with retry and idempotency considerations because message publication and consumption are separate reliability boundaries.

## Caching

Redis is used with a cache-aside strategy for event reads.

SQL Server remains the source of truth. Mutating operations invalidate relevant cache entries after successful persistence.

Redis is treated as an optional dependency: Redis failure degrades performance but does not make the API unavailable.

## Observability

OpenTelemetry provides distributed tracing across key application boundaries, including:

```text
HTTP
→ Application
→ SQL Server
→ Outbox
→ RabbitMQ
→ Consumer
```

Trace context is persisted with Outbox messages so traces can continue across the durable asynchronous boundary.

Health endpoints distinguish between process liveness and dependency readiness.

## Local Development

TicketFlow supports two complementary local environments.

### .NET Aspire

Aspire is used for local development orchestration of the API and its infrastructure dependencies.

### Docker Compose

Docker Compose provides a fully containerized application environment containing:

```text
api
sql
redis
rabbitmq
jaeger
```

SQL Server uses persistent storage and health checks. Redis is intentionally not a startup requirement for the API.

## Persistence

SQL Server is the primary application database and Entity Framework Core owns persistence and migrations.

EF Core migrations are owned by `TicketFlow.Infrastructure`.

Typical commands:

```bash
dotnet ef migrations add <MigrationName> \
  --project TicketFlow.Infrastructure \
  --startup-project TicketFlow.Presentation

dotnet ef database update \
  --project TicketFlow.Infrastructure \
  --startup-project TicketFlow.Presentation
```

## Testing

The test suite combines unit and integration testing across Application and Infrastructure boundaries.

Integration tests use real infrastructure where the behavior matters:

- SQL Server via Testcontainers for persistence, transactions, constraints, and concurrency
- Redis via Testcontainers for cache integration
- MassTransit test infrastructure for consumer/retry behavior

Each SQL-backed test application uses an isolated database, allowing persistence behavior to be tested without depending on a developer's local environment.

Run the complete suite with:

```bash
dotnet test
```

## Phase History

| Phase | Focus | Status |
|---|---|---|
| Phase 1 | Working ASP.NET Core Web API / MVP | Complete |
| Phase 1.5 | Production foundation and hardening | Complete |
| Phase 2 | Clean / modular architecture refactor | Complete |
| Phase 3 | Production backend and infrastructure | Complete* |
| Phase 4 | Product feature expansion | In Progress |
| Phase 5 | Azure and CI/CD | Planned |

\* Resource/ownership authorization is planned alongside the Phase 4 booking modification features.

Historical documentation is available under `docs/phase-1` and `docs/phase-2`.

## Current Roadmap

Phase 4 expands the product behavior using the infrastructure established in Phase 3:

```text
4.1 Update Event       In Progress
4.2 Cancel Booking     Planned
4.3 Update Booking     Planned
4.4 Cancel Event       Planned
4.5 Past Event Photos  Planned
```

The current Update Event feature includes Admin authorization, CQRS validation and handling, transactional persistence of Event changes with integration events through the Outbox Pattern, Redis cache invalidation, and asynchronous event-update notification processing.

Phase 5 will focus on Azure infrastructure and CI/CD, including container deployment and automated delivery pipelines.

## Documentation

- [Phase 1 README](docs/phase-1/README.md)
- [Phase 1 Architecture](docs/phase-1/ARCHITECTURE.md)
- [Phase 2 README](docs/phase-2/README.md)
- [Phase 2 Architecture](docs/phase-2/ARCHITECTURE.md)
