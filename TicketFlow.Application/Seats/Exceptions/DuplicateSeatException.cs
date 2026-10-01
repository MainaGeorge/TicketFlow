namespace TicketFlow.Application.Seats.Exceptions;

public class DuplicateSeatException() :  Exception("One or more seats already exist for this event")
{
}
