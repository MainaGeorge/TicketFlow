using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TicketFlow.Infrastructure.Observability;

public static class TicketFlowTelemetry
{
    public const string SourceName = "TicketFlow.Infrastructure";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> OutboxProcessed = Meter.CreateCounter<long>(
        name: "ticketflow.outbox.processing.processed",
        unit: "{message}", 
        description: "Number of successfully processed outbox messages.");

    public static readonly Counter<long> OutboxProcessingFailures = Meter.CreateCounter<long>(
        name: "ticketflow.outbox.processing.failures",
        unit: "{attempt}",
        description: "Number of failed outbox processing attempts.");

    public static readonly Histogram<double> OutboxProcessingDuration = Meter.CreateHistogram<double>(
        name: "ticketflow.outbox.processing.duration",
        unit: "ms",
        description: "Duration of outbox message processing.");

    public static readonly Counter<long> BookingsCreated = Meter.CreateCounter<long>(
        name: "ticketflow.bookings.created",
        unit: "{booking}",
        description: "Number of successfully created bookings.");
}
