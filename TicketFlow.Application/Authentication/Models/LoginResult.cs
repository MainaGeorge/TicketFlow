using TicketFlow.Contracts.Authentication;

namespace TicketFlow.Application.Authentication.Models;

public abstract record LoginResult;
public record LoginSucceeded(TokenResponse Tokens) : LoginResult;
public record InvalidCredentials : LoginResult;
