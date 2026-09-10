using TicketFlow.Contracts.DTOs;

namespace TicketFlow.Application.Authentication.Models;

public abstract record LoginResult;
public record LoginSucceeded(TokenResponse Tokens) : LoginResult;
public record InvalidCredentials : LoginResult;
