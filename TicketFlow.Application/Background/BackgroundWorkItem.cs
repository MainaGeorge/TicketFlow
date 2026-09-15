namespace TicketFlow.Application.Background;

public sealed record BackgroundWorkItem(string JobType, string Payload, Func<IServiceProvider, CancellationToken, ValueTask> ExecuteAsync);