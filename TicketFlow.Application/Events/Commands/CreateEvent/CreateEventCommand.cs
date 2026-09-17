using MediatR;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Commands.CreateEvent;

public sealed record CreateEventCommand(string Name, string Venue, DateTime Date, string UserId) : IRequest<EventBaseResult>;
