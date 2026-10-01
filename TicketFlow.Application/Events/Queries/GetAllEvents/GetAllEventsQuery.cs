using MediatR;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Queries.GetAllEvents;

public sealed record GetAllEventsQuery : IRequest<IEnumerable<EventResult>>;