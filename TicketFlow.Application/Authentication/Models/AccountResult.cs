namespace TicketFlow.Application.Authentication.Models;

public abstract record AccountResult;
public record AccountNotFound : AccountResult;
public record AccountDeactivated : AccountResult;
public record AccountReactivated : AccountResult;
public record AccountActivationFailed(IReadOnlyCollection<IdentityError>? Errors) : AccountResult;
public record AccountDeActivationFailed(IReadOnlyCollection<IdentityError>? Errors) : AccountResult;
public record AccountAlreadyActivated : AccountResult;
public record AccountAlreadyDeactivated : AccountResult;
