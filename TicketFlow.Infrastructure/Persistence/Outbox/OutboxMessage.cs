namespace TicketFlow.Infrastructure.Persistence.Outbox;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public OutboxMessageType MessageType { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public int RetryCount { get; private set; }

    public string? TraceParent { get; private set; }
    public string? TraceState { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payload, OutboxMessageType messageType, string? traceParent = null, string? traceState = null)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        OccurredAt = DateTimeOffset.UtcNow;
        MessageType = messageType;
        TraceParent = traceParent;
        TraceState = traceState;
    }

    public void MarkProcessed() => ProcessedAt ??= DateTimeOffset.UtcNow;
    public void RecordFailure() => RetryCount++;
    public void MarkFailed() => FailedAt ??= DateTimeOffset.UtcNow;
    
}