using Microsoft.EntityFrameworkCore;
using TicketFlow.Models;

namespace TicketFlow.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Seat>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<Booking>()
            .HasKey(b => b.Id);

        modelBuilder.Entity<Event>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<Event>()
            .HasMany(e => e.Seats)
            .WithOne(s => s.Event)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Bookings)
            .WithOne(u => u.User)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Seat>()
            .HasOne(p => p.Booking)
            .WithOne(b => b.Seat)
            .HasForeignKey<Booking>(s => s.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
