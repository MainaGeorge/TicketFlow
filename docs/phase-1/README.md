# TicketFlow – Phase 1 / 1.5

## Overview

Phase 1 established TicketFlow as a working ASP.NET Core Web API. Phase 1.5 added the production-oriented foundation needed before introducing a modular architecture.

The goal was to prove the core ticket-booking domain and establish reliable API behavior, persistence, authentication, validation, logging, and testing.

## Phase 1 Scope

The initial application provided:

- ASP.NET Core Web API
- SQL Server persistence
- Entity Framework Core
- ASP.NET Core Identity
- JWT authentication
- Events
- Seats
- Bookings
- Mock payment/email behavior
- Basic concurrency protection for seat booking

### Core relationships

- An `Event` has many `Seat` records.
- A `User` has many `Booking` records.
- A `Booking` belongs to one `Seat`.
- A seat can have at most one booking.

Seat availability is derived from the booking relationship rather than maintaining duplicate booking state on `Seat`.

## Phase 1.5 Foundation

Phase 1.5 added:

- Global exception handling with `ProblemDetails`
- Request validation
- Business validation and consistent HTTP status semantics
- Structured logging with `ILogger` and Serilog
- Swagger/OpenAPI bearer authentication support
- API versioning
- Authentication hardening
- User activation/deactivation
- SQL Server uniqueness protection for concurrent booking attempts
- Integration tests
- Controller unit tests
- Application service unit tests
- Authentication, Identity, and token infrastructure tests
- Refresh-token persistence foundation

## Booking Concurrency

The booking flow first checks whether a seat is already booked, while the database unique constraint remains the final protection against simultaneous booking attempts.

Duplicate-key database errors are treated as a booking conflict at the application boundary.

## Testing

Phase 1/1.5 tests cover:

- Authentication
- Events
- Seats
- Bookings
- Ownership/security behavior
- Validation
- Concurrent booking attempts

The project uses SQL Server-backed integration tests rather than relying exclusively on an in-memory provider.

## Phase 1.5 Boundary

The full refresh-token renewal/rotation endpoint and distributed/cloud-native capabilities were intentionally left for later phases.

The next architectural step was Phase 2: extracting clear Domain, Application, Infrastructure, Contracts, and Presentation boundaries.
