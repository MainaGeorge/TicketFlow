using MediatR;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string Token) : IRequest<RefreshTokenResult>;
