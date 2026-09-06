using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Authentication.Models;

public abstract record RegisterResult;
public record RegistrationSucceeded(User User) : RegisterResult;
public record EmailAlreadyRegistered : RegisterResult;
public record RegistrationFailed(IReadOnlyCollection<IdentityError> Errors) : RegisterResult;
