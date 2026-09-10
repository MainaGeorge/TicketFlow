using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Authentication.Models;

public record IdentityCreationResult(bool Success, User? User, IReadOnlyCollection<IdentityError>? Errors = null);
public record IdentityUpdateResult(bool Success, IReadOnlyCollection<IdentityError>? Errors = null);
