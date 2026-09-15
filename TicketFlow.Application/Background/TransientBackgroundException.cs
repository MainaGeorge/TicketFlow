namespace TicketFlow.Application.Background;

public class TransientBackgroundException : Exception
{
    public TransientBackgroundException(string message) : base(message)
    {
    }

    public TransientBackgroundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}