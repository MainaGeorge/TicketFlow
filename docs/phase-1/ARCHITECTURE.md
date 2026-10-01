# TicketFlow – Phase 1 / 1.5 Architecture

## Architectural Style

Phase 1 was implemented as a modular monolith: the application was deployed as a single API, with the domain, persistence, authentication, and HTTP layers still living close together.

Phase 1.5 strengthened the monolith with production-oriented API behavior and test coverage.

```text
Client
  |
  v
ASP.NET Core API
  |
  +-- Authentication
  +-- Events
  +-- Seats
  +-- Bookings
  |
  v
Entity Framework Core
  |
  v
SQL Server
```

## Domain Model

```text
Event
  |
  +----< Seat
             |
             +---- 0..1 Booking >---- User
```

Key rules:

- An event owns its seats.
- A booking belongs to one user.
- A booking references one seat.
- A unique constraint protects the one-booking-per-seat rule.

## Authentication

ASP.NET Core Identity manages users and passwords.

JWT access tokens are issued after successful authentication.

The Phase 1.5 authentication foundation also introduced refresh-token persistence, but the complete refresh-token renewal lifecycle was not yet implemented.

## Error Handling

The API uses:

- `400 Bad Request` for malformed/invalid request data and business validation failures
- `401 Unauthorized` for missing or invalid authentication
- `404 Not Found` for missing resources
- `409 Conflict` for state conflicts such as a seat already being booked
- `ProblemDetails` for API error responses
- A global exception handler for unexpected failures

## Logging

Serilog is used with ASP.NET Core request logging and structured properties such as trace identifiers and user identifiers where appropriate.

Sensitive values such as passwords, JWTs, refresh tokens, and authorization headers are not logged.

## Testing

Phase 1.5 established multiple levels of testing:

```text
Controller unit tests
        |
Application/service unit tests
        |
Integration tests
        |
SQL Server
```

The integration layer is particularly important for persistence and concurrency behavior.

## Limitations of Phase 1

The original structure allowed controllers and application logic to access infrastructure concerns directly. This made the boundaries less explicit and made some responsibilities harder to isolate.

Phase 2 addresses those limitations through project-level separation and dependency inversion.
