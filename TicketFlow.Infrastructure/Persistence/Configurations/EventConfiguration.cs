using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

internal class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder
            .HasKey(p => p.Id);

        builder
            .Property(p => p.Id)
            .IsRequired();

        builder
            .Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(e => e.Venue)
            .HasMaxLength(200)
            .IsRequired();

        builder
            .Property(e => e.EventDate)
            .IsRequired();

        builder
            .HasMany(e => e.Seats)
            .WithOne(s => s.Event)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
