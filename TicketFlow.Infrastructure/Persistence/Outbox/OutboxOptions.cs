namespace TicketFlow.Infrastructure.Persistence.Outbox;

public class OutboxOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int PollingIntervalSeconds { get; set; } = 5;
}
