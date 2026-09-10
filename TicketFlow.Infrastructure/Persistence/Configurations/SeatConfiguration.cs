using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

internal class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        {
            builder.HasKey(s => s.Id);

            builder
                .Property(s => s.Row)
                .IsRequired()
                .HasMaxLength(10);

            builder
                .Property(s => s.Price)
                .HasColumnType("decimal(18,2)");

            builder
                .HasOne(s => s.Booking)
                .WithOne(b => b.Seat)
                .HasForeignKey<Booking>(b => b.SeatId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
