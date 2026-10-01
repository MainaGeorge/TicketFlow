namespace TicketFlow.Domain.Background;

public class FailedBackgroundJob
{
    public int Id { get; set; }

    public string JobType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string FailureReason { get; set; } = string.Empty;

    public string? ExceptionType { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset FailedAt { get; set; }
}