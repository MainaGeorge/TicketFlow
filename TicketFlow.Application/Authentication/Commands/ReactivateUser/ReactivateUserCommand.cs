using MediatR;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.ReactivateUser;

public sealed record ReactivateUserCommand(string Email) : IRequest<AccountResult>;
