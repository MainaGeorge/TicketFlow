# TicketFlow

TicketFlow is a production-oriented .NET Web API for event ticket booking. The project starts as a modular monolith and evolves through staged architecture phases toward a more scalable, maintainable backend.

## Current Status

**Phase 2 – Modular Architecture**

The current codebase is split into:

- `TicketFlow.Domain`
- `TicketFlow.Application`
- `TicketFlow.Contracts`
- `TicketFlow.Infrastructure`
- `TicketFlow.Presentation`
- `TicketFlow.Tests`

The API currently covers authentication, events, seats, bookings, JWT access tokens, refresh-token persistence, validation, structured logging, API versioning, Swagger/OpenAPI, concurrency protection, and automated tests.

## Phase History

| Phase | Focus | Status |
|---|---|---|
| Phase 1 | Working ASP.NET Core Web API / MVP | Complete |
| Phase 1.5 | Production foundation and hardening | Complete |
| Phase 2 | Modular architecture refactor | Complete |

See the historical documentation in [`docs/phase-1`](docs/phase-1) and [`docs/phase-2`](docs/phase-2).

## Documentation

- [Phase 1 README](docs/phase-1/README.md)
- [Phase 1 Architecture](docs/phase-1/ARCHITECTURE.md)
- [Phase 2 README](docs/phase-2/README.md)
- [Phase 2 Architecture](docs/phase-2/ARCHITECTURE.md)

## Development

SQL Server is used for persistence and Entity Framework Core owns data access and migrations.

EF Core migrations are owned by `TicketFlow.Infrastructure`.

Typical commands:

```bash
dotnet ef migrations add <MigrationName>   --project TicketFlow.Infrastructure   --startup-project TicketFlow.Presentation

dotnet ef database update   --project TicketFlow.Infrastructure   --startup-project TicketFlow.Presentation
```

## Testing

The test suite contains controller unit tests, Application service unit tests, Infrastructure tests, and SQL Server-backed integration tests.

```bash
dotnet test
```

## Roadmap

The next phase can build on this modular foundation with capabilities such as a complete refresh-token lifecycle, messaging/background processing, stronger observability, and cloud-oriented infrastructure.
