using MediatR;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;
