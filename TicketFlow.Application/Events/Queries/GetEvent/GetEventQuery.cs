using MediatR;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Queries.GetEvent;

public sealed record GetEventQuery(int Id) : IRequest<EventBaseResult>;
