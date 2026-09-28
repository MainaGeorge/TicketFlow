using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Infrastructure.Persistence.Outbox;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(x => x.TraceParent)
             .HasMaxLength(128)
             .IsUnicode(false);

        builder.Property(x => x.TraceState)
            .HasMaxLength(512)
            .IsUnicode(false);
    }
}
