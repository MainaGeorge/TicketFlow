using MediatR;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands;

public sealed record RegisterCommand(string Email, string Password, string DisplayName) : IRequest<RegisterResult>;
