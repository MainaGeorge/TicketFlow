using TicketFlow.Contracts.DTOs;

namespace TicketFlow.Application.Authentication.Models;

public abstract record RefreshTokenResult;
public record RefreshTokenSucceeded(TokenResponse Tokens) : RefreshTokenResult;
public record RefreshTokenInvalid : RefreshTokenResult;
public record RefreshTokenRevoked : RefreshTokenResult;
public record RefreshTokenExpired : RefreshTokenResult;
