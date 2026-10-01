# TicketFlow – Phase 3 Architecture

## Overview

Phase 3 preserves the layered modular architecture established in Phase 2 and adds production-oriented infrastructure around it.

The compile-time dependency direction remains:

```text
Domain
  ^
  |
Application ----> Contracts
  ^
  |
Infrastructure
  ^
  |
Presentation
```

Infrastructure implements Application abstractions, while Presentation remains the composition root.

The application is still a modular monolith. RabbitMQ and Redis are infrastructure dependencies, not separate TicketFlow application services.

## System Architecture

```text
                         Client
                           |
                           v
                 TicketFlow.Presentation
                 Controllers / HTTP API
                           |
                           v
                  TicketFlow.Application
              Commands / Queries / Use Cases
                    /       |        \
                   v        v         v
              Domain    Abstractions  Events
                           |
                           v
                TicketFlow.Infrastructure
             /          |          |         \
            v           v          v          v
       SQL Server     Redis     RabbitMQ   Identity/JWT
            |                       |
            |                       v
            |              MassTransit Consumer
            |                       |
            +-----------------------+
                    durable work
```

The central rule remains that Application depends on abstractions, while Infrastructure owns framework-specific implementations.

## Application Boundaries

### Domain

Owns core entities and domain concepts.

```text
Domain -> none
```

Examples include:

- `Event`
- `Seat`
- `Booking`
- `User`
- `RefreshToken`

### Application

Owns use cases, business decisions, commands/queries, event handling, and infrastructure abstractions.

```text
Application -> Domain
Application -> Contracts
```

Important abstractions include:

```text
Authentication/
├── IIdentityService
└── ITokenService

Caching/
└── ICacheService

Messaging/
└── IIntegrationEventPublisher

Repository/
├── IBookingRepository
├── IEventsRepository
└── IRefreshTokenRepository
```

Application code does not depend directly on EF Core, Redis clients, RabbitMQ clients, ASP.NET Identity implementation types, or JWT implementation types.

### Contracts

Owns API and integration transport contracts.

Contracts remain independent of Infrastructure concerns.

### Infrastructure

Owns technical implementations, including:

- EF Core and `AppDbContext`
- SQL Server repositories
- ASP.NET Core Identity adapters
- JWT generation
- Refresh-token persistence
- Redis cache implementation
- Outbox persistence and processing
- MassTransit/RabbitMQ publishing and consumers
- Background processing
- OpenTelemetry instrumentation
- Health-check integrations

### Presentation

Owns the HTTP boundary and application composition:

- Controllers
- Authentication/authorization configuration
- HTTP result translation
- Middleware
- Swagger/OpenAPI
- Health endpoints
- Dependency injection
- Application startup

## Authentication and Authorization

Authentication remains separated behind Application abstractions:

```text
AuthenticationController
          |
          v
Authentication Use Case
       /          \
      v            v
IIdentityService  ITokenService
      |            |
      v            v
IdentityService  TokenService
      |            |
      v            +--> JWT
ASP.NET Identity   |
                   +--> RefreshToken / SQL Server
```

Phase 3 adds role-based authorization.

```text
Anonymous
   |
   +--> public reads

Authenticated User
   |
   +--> authenticated booking operations

Admin
   |
   +--> event administration
   +--> seat administration
```

Roles are determined by the server. Public registration does not accept a client-selected role.

Resource/ownership authorization is intentionally reserved for booking operations that require an existing resource, such as future update and cancellation endpoints.

## CQRS

Application behavior is organized into commands and queries with MediatR.

```text
HTTP Request
     |
     v
Controller
     |
     v
Command / Query
     |
     v
MediatR
     |
     v
Handler
     |
     v
Application abstractions
     |
     v
Infrastructure
```

CQRS is used to make use-case boundaries explicit. It does not imply separate databases or microservices.

## Domain and Integration Events

TicketFlow distinguishes between events that remain inside the application and events that cross a durable infrastructure boundary.

```text
Domain/Application Event
        |
        +--> in-process application coordination

Integration Event
        |
        +--> Outbox
               |
               v
            Broker
               |
               v
           Consumer
```

This avoids treating every application event as a distributed message.

## Booking Transaction and Outbox

The Outbox pattern addresses the dual-write problem between SQL Server and RabbitMQ.

Without an Outbox, saving a booking and publishing an event are two independent operations. A failure between them could leave persisted state without the corresponding message.

TicketFlow instead persists both in one SQL transaction:

```text
Create Booking Command
        |
        v
Booking Handler / Service
        |
        v
Repository
        |
        v
+-----------------------------+
|      SQL Transaction        |
|                             |
|  Save Booking               |
|       |                     |
|       v                     |
|  obtain BookingId           |
|       |                     |
|       v                     |
|  create Integration Event   |
|       |                     |
|       v                     |
|  save OutboxMessage         |
|                             |
+-----------------------------+
        |
        v
      Commit
```

The booking and publication intent therefore succeed or fail together.

## Outbox Processing and RabbitMQ

After the database transaction commits, asynchronous processing continues independently:

```text
SQL Server
    |
    | pending OutboxMessage
    v
Outbox Processor
    |
    | IIntegrationEventPublisher
    v
MassTransit
    |
    v
RabbitMQ
    |
    v
BookingConfirmationConsumer
    |
    v
BookingConfirmationProcessor
```

Outbox message state follows:

```text
ProcessedAt == null && FailedAt == null
    -> pending / retryable

ProcessedAt != null
    -> successfully published

FailedAt != null
    -> terminal / exhausted
```

The Outbox guarantees durable publication intent, not exactly-once processing.

A publish may reach RabbitMQ and then fail before `ProcessedAt` is saved. The message can therefore be published again.

TicketFlow consequently assumes at-least-once delivery.

## Consumer Retry and Idempotency

MassTransit handles short-lived consumer retry behavior.

```text
RabbitMQ
    |
    v
Consumer
    |
    +--> attempt
    +--> retry 1
    +--> retry 2
    +--> retry 3
    |
    +--> success
          or
         _error
```

With a retry count of three, the consumer can be invoked four times: the original attempt plus three retries.

Retry does not replace idempotency. `IBookingConfirmationProcessor` checks processed work so duplicate message delivery does not repeat already completed side effects.

The producer Outbox lifecycle and the consumer retry/error lifecycle are separate reliability boundaries.

## Redis Cache Architecture

Redis is hidden behind `ICacheService`.

Event reads use cache-aside:

```text
GetEvent Query
      |
      v
ICacheService
      |
      v
Redis
  |
  +-- hit ------------------> return event
  |
  +-- miss
       |
       v
 IEventsRepository
       |
       v
   SQL Server
       |
       v
 cache result
       |
       v
 return event
```

SQL Server remains authoritative.

Redis is intentionally optional. Connection configuration allows the application to start when Redis is unavailable, and health reporting treats Redis failure as degraded rather than not-ready.

## Seat Persistence Invariant

Bulk seat creation relies on both application validation and a database constraint.

Canonical seat identity is:

```text
EventId + normalized Row + Number
```

Row normalization is:

```text
Trim -> ToUpperInvariant
```

SQL Server enforces:

```text
UNIQUE(EventId, Row, Number)
```

This protects the invariant even under concurrent requests where application-level duplicate checks alone would be insufficient.

## OpenTelemetry

OpenTelemetry observes both synchronous and asynchronous execution.

```text
                     +----------------+
HTTP Request ------->| ASP.NET Core   |
                     +-------+--------+
                             |
                             v
                     Application Work
                       /           \
                      v             v
                SQL Server        Redis
                      |
                      v
                 OutboxMessage
               TraceParent/State
                      |
              durable time gap
                      |
                      v
               Outbox Processor
                      |
                      v
                   RabbitMQ
                      |
                      v
                   Consumer
                      |
                      v
                    Work

All telemetry ----------------------> Jaeger
```

The durable Outbox boundary is important because normal in-memory activity context does not survive database persistence and later background execution.

TicketFlow stores trace context with the Outbox message and restores propagation when the message is published.

## Health Architecture

Health endpoints have different meanings:

```text
/health/live
    |
    +--> Is the process alive?

/health/ready
    |
    +--> SQL Server required
    +--> RabbitMQ required
    +--> Redis not required

/health
    |
    +--> aggregate dependency status
```

Expected behavior:

| Dependency failure | Live | Ready | Overall |
| --- | --- | --- | --- |
| None | Healthy | Healthy | Healthy |
| SQL Server | Healthy | Unhealthy | Unhealthy |
| Redis | Healthy | Healthy | Degraded |
| RabbitMQ | Healthy | Unhealthy | Unhealthy |

This prevents an optional cache outage from making the API unavailable while still exposing the degraded state.

## Runtime Environments

Phase 3 deliberately keeps orchestration concerns separate.

### .NET Aspire

Used for local development orchestration:

```text
Aspire AppHost
├── TicketFlow API
├── SQL Server
│   └── TicketFlow database
├── Redis
└── RabbitMQ
```

Aspire improves the local development experience without becoming a requirement for running the automated test suite.

### Docker Compose

Used for the fully containerized application environment:

```text
Docker Compose
├── api
├── sql
├── redis
├── rabbitmq
└── jaeger
```

The API waits for required SQL readiness. Redis remains an optional dependency.

### Testcontainers

Used by automated integration tests:

```text
xUnit
 |
 +--> IntegrationTestFixture
        |
        +--> SQL Server container
        |      |
        |      +--> TicketFlow_Test_<guid>
        |
        +--> Redis container
```

The SQL Server container can be shared while each `CustomWebApplicationFactory` owns an isolated database.

Database cleanup is independent from the application host: the host is disposed first, pooled SQL connections are cleared, and the isolated test database is then removed.

MassTransit consumer/retry tests use the MassTransit Test Harness rather than a real RabbitMQ container because those tests verify consumer middleware behavior, not RabbitMQ transport behavior.

## Testing Architecture

```text
Unit Tests
   |
   +--> Application behavior with mocked abstractions
   |
   +--> HTTP/result mapping where appropriate

Infrastructure / Component Tests
   |
   +--> MassTransit Test Harness
   +--> infrastructure adapters

Integration Tests
   |
   +--> WebApplicationFactory
           |
           +--> real SQL Server via Testcontainers
           +--> real Redis where Redis behavior is under test
```

The test suite chooses the narrowest useful boundary.

A real external dependency is used when its behavior is part of what the test needs to prove. Infrastructure is not added merely for symmetry.

## Phase 3 Boundary

Phase 3 completes the production-backend infrastructure milestone.

Included:

- Refresh-token lifecycle
- Role-based authorization
- CQRS with MediatR
- Domain/application events
- Transactional Outbox
- RabbitMQ with MassTransit
- Consumer retry and idempotency
- Redis caching
- Bulk seat creation
- OpenTelemetry
- Health checks
- Docker and Docker Compose
- .NET Aspire
- SQL Server and Redis Testcontainers

Deliberately deferred:

- Resource/ownership authorization for future booking mutations
- Update event
- Cancel/update booking
- Cancel event
- Past-event photos
- Azure infrastructure
- CI/CD and deployment

Phase 4 can build product features on top of this infrastructure without requiring another architectural rewrite.
