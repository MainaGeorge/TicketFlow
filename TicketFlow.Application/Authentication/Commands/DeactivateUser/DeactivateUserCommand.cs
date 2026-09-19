using MediatR;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(string Email) : IRequest<AccountResult>;
