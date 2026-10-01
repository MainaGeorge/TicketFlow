# TicketFlow – Phase 3

## Overview

Phase 3 evolves TicketFlow from the Phase 2 modular monolith into a production-oriented backend while preserving the same architectural boundaries.

The focus of this phase is reliability, security, asynchronous processing, observability, operational readiness, local orchestration, and automated integration-test infrastructure.

The application remains a modular monolith. Distributed infrastructure is introduced only where it solves a concrete problem.

## Capabilities Added

Phase 3 introduces:

- Refresh-token lifecycle management
- Role-based authorization
- CQRS with MediatR
- Domain and application events
- Transactional Outbox pattern
- RabbitMQ messaging with MassTransit
- Idempotent background processing
- Redis distributed caching
- Bulk seat creation with database-enforced uniqueness
- OpenTelemetry tracing and metrics
- Health and readiness checks
- Docker and Docker Compose
- .NET Aspire for local development orchestration
- Testcontainers for automated SQL Server and Redis integration dependencies

Resource/ownership authorization for future booking update and cancellation endpoints remains planned for the product-feature phase.

## Architecture

The Phase 2 project boundaries remain in place:

```text
TicketFlow.Domain
TicketFlow.Application
TicketFlow.Contracts
TicketFlow.Infrastructure
TicketFlow.Presentation
TicketFlow.Tests
```

The dependency direction remains:

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

Infrastructure implements Application abstractions for persistence, identity, caching, messaging, and other technical concerns. Presentation remains the composition root.

See `ARCHITECTURE.md` for the detailed Phase 3 architecture and processing flows.

## Authentication and Authorization

Authentication uses ASP.NET Core Identity, JWT access tokens, and refresh tokens.

Phase 3 completes the refresh-token lifecycle and introduces application roles:

```text
Anonymous
    |
    +--> Read events and available seats

Authenticated User
    |
    +--> Read events and seats
    +--> Create bookings

Admin
    |
    +--> User capabilities
    +--> Create/manage events
    +--> Manage event seats
```

Public registration never accepts a role from the client. New accounts receive the normal User role, while administrative privileges are assigned separately.

JWT role claims are rebuilt from the user's current roles when tokens are issued or refreshed.

## CQRS and Events

Application use cases are organized around commands and queries using MediatR.

Domain/application events provide in-process decoupling where work belongs to the same application boundary.

Integration events are used when work crosses the durable messaging boundary.

This distinction keeps internal application coordination separate from broker-based communication.

## Transactional Outbox and Messaging

Booking creation uses the Outbox pattern so the booking and the intent to publish its integration event are committed atomically to SQL Server.

```text
Create Booking
      |
      v
SQL Transaction
   /        \
  v          v
Booking   OutboxMessage
              |
              v
       Outbox Processor
              |
              v
          RabbitMQ
              |
              v
BookingConfirmationConsumer
              |
              v
Background Processing
```

The Outbox provides at-least-once delivery semantics. Publishing can succeed before marking an Outbox message as processed, so consumers must tolerate duplicate delivery.

Booking confirmation processing therefore uses idempotency to avoid repeating already completed work.

MassTransit provides the RabbitMQ transport and consumer retry behavior. Failed consumer processing follows MassTransit's retry/error-queue lifecycle independently from the producer Outbox.

## Redis Caching

Redis is used through an Application abstraction rather than directly from use cases.

Event reads use cache-aside behavior:

```text
Get Event
    |
    v
Redis lookup
   /     \
 hit     miss
  |        |
  v        v
return   SQL Server
           |
           v
       cache result
           |
           v
         return
```

SQL Server remains the source of truth.

Redis is treated as an optional dependency. A Redis outage should degrade caching rather than prevent the API from starting.

## Bulk Seat Creation

Phase 3 adds administrative bulk seat creation.

Seat identity is unique within an event using:

```text
EventId + normalized Row + Number
```

Rows are normalized before persistence, and SQL Server enforces the final uniqueness invariant with a unique index.

Bulk creation is atomic: either the requested seats are persisted together or the operation fails without partially creating the set.

## Observability

OpenTelemetry provides tracing and metrics across the application and infrastructure.

Tracing covers:

- ASP.NET Core requests
- SQL Server
- Redis
- MassTransit/RabbitMQ
- Custom TicketFlow activities

Trace context is persisted with Outbox messages so a booking request can be correlated across the durable asynchronous boundary:

```text
HTTP Request
     |
     v
Application / SQL
     |
     v
OutboxMessage
  TraceParent
  TraceState
     |
     v
Outbox Processor
     |
     v
RabbitMQ
     |
     v
Consumer
```

Jaeger is used locally to inspect distributed traces.

## Health Checks

TicketFlow exposes separate liveness, readiness, and overall health endpoints:

```text
/health/live
/health/ready
/health
```

The intended dependency behavior is:

| Failure | `/health/live` | `/health/ready` | `/health` |
| --- | --- | --- | --- |
| None | Healthy | Healthy | Healthy |
| SQL Server unavailable | Healthy | Unhealthy | Unhealthy |
| Redis unavailable | Healthy | Healthy | Degraded |
| RabbitMQ unavailable | Healthy | Unhealthy | Unhealthy |

This reflects the dependency model: SQL Server and RabbitMQ are required for readiness, while Redis is an optional performance dependency.

## Local Development and Containers

Phase 3 provides two complementary application-development environments.

`.NET Aspire` is used for local development orchestration and service discovery.

`Docker Compose` runs the complete containerized stack:

```text
Docker Compose
├── api
├── sql
├── redis
├── rabbitmq
└── jaeger
```

These approaches serve different purposes rather than replacing each other.

## Automated Integration Tests

Integration tests no longer require SQL Server or Redis to be started manually.

Testcontainers provisions the required infrastructure:

```text
dotnet test
    |
    +--> SQL Server Testcontainer
    |       |
    |       +--> isolated TicketFlow_Test_<guid> database
    |
    +--> Redis Testcontainer
    |
    +--> MassTransit Test Harness
            |
            +--> consumer and retry behavior
```

SQL Server is shared at the container level while each application factory receives an isolated database.

Redis adapter tests use the real Redis protocol and server rather than a mock.

The MassTransit Test Harness tests consumer and retry behavior without requiring a RabbitMQ Testcontainer. A real RabbitMQ container should only be introduced if tests need to verify actual broker transport, topology, or routing behavior.

## Phase 3 Outcome

Phase 3 turns the modular foundation from Phase 2 into an operationally mature backend.

TicketFlow now demonstrates transactional consistency across asynchronous work, durable messaging, idempotent consumers, distributed caching, authorization, tracing, health checks, containerized development environments, and reproducible integration-test infrastructure.

The next phase returns the focus to product functionality, including event and booking update/cancellation workflows and past-event photos.
