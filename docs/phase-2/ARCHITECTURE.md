# TicketFlow – Phase 2 Architecture

## Overview

Phase 2 uses a layered modular architecture with explicit dependency direction.

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

The important compile-time direction is that Infrastructure implements Application abstractions, while Presentation acts as the composition root.

## TicketFlow.Domain

Owns core domain entities and domain-level concepts.

Dependencies:

```text
Domain -> none
```

Examples:

- `Event`
- `Seat`
- `Booking`
- `User`
- `RefreshToken`

## TicketFlow.Application

Owns application behavior and use cases.

Dependencies:

```text
Application -> Domain
Application -> Contracts
```

Examples:

- `BookingsService`
- `EventsService`
- `SeatsService`
- `AuthenticationService`
- Repository interfaces
- Identity/token abstractions
- Application result models

Application code does not reference EF Core, `AppDbContext`, `UserManager<User>`, SQL Server exceptions, or JWT implementation types.

## TicketFlow.Contracts

Contains transport contracts used by the API:

- Request DTOs
- Response DTOs such as `TokenResponse`

Contracts remain independent of Domain and Infrastructure concerns.

## TicketFlow.Infrastructure

Implements Application abstractions and owns technical infrastructure.

Dependencies:

```text
Infrastructure -> Application
Infrastructure -> Domain
```

Examples:

- `AppDbContext`
- EF Core configurations
- Repositories
- EF Core migrations
- `IdentityService`
- `TokenService`

`IdentityService` adapts ASP.NET Core Identity to `IIdentityService`.

`TokenService` handles JWT creation and refresh-token persistence behind `ITokenService`.

## Persistence

The persistence flow is:

```text
Application Service
      |
      v
Repository Interface
      |
      v
Repository Implementation
      |
      v
AppDbContext
      |
      v
SQL Server
```

`AppDbContext` and migrations are located in Infrastructure.

## Authentication

Authentication follows:

```text
AuthenticationController
          |
          v
IAuthenticationService
          |
          v
AuthenticationService
       /               v           v
IIdentityService ITokenService
      |           |
      v           v
IdentityService TokenService
      |           |
      v           +--> JWT
UserManager          |
                     +--> RefreshToken/AppDbContext
```

The Application service owns the decisions involved in registration, login, activation, and deactivation.

Infrastructure owns the framework-specific implementation.

## Presentation

Presentation owns:

- Controllers
- HTTP status codes
- ProblemDetails/ValidationProblemDetails translation
- API versioning
- Swagger/OpenAPI
- Authentication configuration
- Middleware
- Composition root

A controller should generally follow:

```text
HTTP request
    |
    v
Application service
    |
    v
Application result
    |
    v
HTTP response
```

## Dependency Injection

Presentation composes the application:

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

Infrastructure provides the concrete implementations required by Application.

## Database Migrations at Startup

Infrastructure exposes a migration operation, while Presentation decides when to run it.

Conceptually:

```text
Presentation startup
      |
      | Development?
      v
Infrastructure migration method
      |
      v
AppDbContext.Database.MigrateAsync()
```

This keeps migration ownership inside Infrastructure without making Presentation responsible for EF Core persistence details.

## Testing Architecture

```text
Controller Unit Tests
          |
          v
Application Unit Tests
          |
          v
Infrastructure Tests
          |
          v
Integration Tests
          |
          v
SQL Server
```

Each layer tests its own responsibility:

- Controllers test HTTP/result mapping.
- Application tests business/use-case behavior with mocked abstractions.
- Infrastructure tests adapter and persistence behavior.
- Integration tests verify the application working against SQL Server.

## Phase 2 Boundary

Phase 2 deliberately stops short of introducing distributed infrastructure.

Not yet included:

- Full refresh-token rotation/renewal workflow
- Message broker/event bus
- Outbox pattern
- Distributed caching
- Background workers
- Cloud deployment
- Distributed tracing

Those capabilities can be introduced incrementally on top of this modular foundation.
