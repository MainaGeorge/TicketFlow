namespace TicketFlow.Application.Background;

public class PermanentBackgroundException : Exception
{
    public PermanentBackgroundException(string message) : base(message)
    {
    }

    public PermanentBackgroundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}