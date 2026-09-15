using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Background;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class FailedBackgroundJobConfiguration : IEntityTypeConfiguration<FailedBackgroundJob>
{
    public void Configure(
        EntityTypeBuilder<FailedBackgroundJob> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .IsRequired();

        builder.Property(x => x.ExceptionType)
            .HasMaxLength(500);

        builder.Property(x => x.AttemptCount)
            .IsRequired();

        builder.Property(x => x.FailedAt)
            .IsRequired();
    }
}