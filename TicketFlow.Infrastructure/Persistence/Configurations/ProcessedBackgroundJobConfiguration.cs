using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Background;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class ProcessedBackgroundJobConfiguration : IEntityTypeConfiguration<ProcessedBackgroundJob>
{
    public void Configure(EntityTypeBuilder<ProcessedBackgroundJob> builder)
    {
        builder
            .Property(x => x.IdempotencyKey)
            .HasMaxLength(500)
            .IsRequired();

        builder
            .HasIndex(x => x.IdempotencyKey)
            .IsUnique();
    }
}
