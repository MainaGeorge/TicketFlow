namespace TicketFlow.Domain.Background;

public class ProcessedBackgroundJob
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}