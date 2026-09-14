namespace TicketFlow.Infrastructure.Background;

public class BackgroundRetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    public double JitterFactor { get; set; } = 0.2;
}