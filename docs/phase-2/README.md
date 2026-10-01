# TicketFlow – Phase 2

## Overview

Phase 2 refactors TicketFlow from the original monolithic project structure into a modular architecture with explicit boundaries between domain logic, application use cases, contracts, infrastructure, and HTTP presentation.

The objective is to improve separation of concerns, testability, and long-term maintainability without changing the core ticket-booking behavior.

## Project Structure

```text
TicketFlow.Domain
TicketFlow.Application
TicketFlow.Contracts
TicketFlow.Infrastructure
TicketFlow.Presentation
TicketFlow.Tests
```

### Domain

Contains core entities and domain concepts.

The Domain project has no dependency on the other application layers.

### Application

Contains application use cases, business rules, service abstractions, repository interfaces, and application result types.

Application does not depend on Infrastructure implementations.

### Contracts

Contains API-facing request and response DTOs.

### Infrastructure

Contains implementations of persistence and external/framework-specific concerns, including:

- `AppDbContext`
- EF Core entity configurations
- EF Core migrations
- Repository implementations
- ASP.NET Core Identity integration
- JWT/token generation
- Refresh-token persistence

### Presentation

Contains controllers, API configuration, middleware, versioning, Swagger/OpenAPI, and dependency injection composition.

## Refactored Application Areas

The following areas now follow the Application service boundary:

- Events
- Seats
- Bookings
- Authentication

Controllers delegate to Application services and translate application results into HTTP responses.

## Authentication Refactor

Authentication now follows:

```text
AuthenticationController
        |
        v
IAuthenticationService
        |
        v
AuthenticationService
       /       /        v     v
IIdentityService   ITokenService
      |                 |
      v                 v
IdentityService    TokenService
      |                 |
      v                 v
UserManager       JWT + RefreshToken + EF Core
```

This prevents the Application layer from depending directly on ASP.NET Identity, JWT implementation details, or EF Core.

## Database and Migrations

EF Core migrations are owned by `TicketFlow.Infrastructure`.

Presentation is the startup/composition root and can trigger Infrastructure's migration mechanism during Development.

Tests can create isolated SQL Server databases and apply the same migrations automatically.

## Testing

Phase 2 adds and organizes tests across the architecture:

```text
TicketFlow.Tests
├── Controllers
├── Application
├── Infrastructure
└── Integration
```

Application tests mock abstractions such as repositories and identity/token services.

Infrastructure tests verify adapters such as Identity and token generation.

SQL Server-backed integration tests remain in place for end-to-end behavior and concurrency.

## Phase 2 Outcome

Phase 2 establishes a modular monolith that can evolve without requiring the API controllers, business logic, persistence, and framework integrations to remain tightly coupled.

The next phase can therefore focus on new capabilities rather than another structural rewrite.
