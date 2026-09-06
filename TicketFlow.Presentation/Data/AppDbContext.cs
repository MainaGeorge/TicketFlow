using Microsoft.EntityFrameworkCore;
using TicketFlow.Presentation.DTOs;
using TicketFlow.Presentation.Models;

namespace TicketFlow.Presentation.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Seat>()
            .HasKey(s => s.Id);
            
        modelBuilder.Entity<Seat>()
            .Property(s => s.Row)
            .IsRequired();

        modelBuilder.Entity<Seat>()
            .Property(s => s.Number)
            .IsRequired();

        modelBuilder.Entity<Seat>()
            .Property(s => s.Price)
            .IsRequired();

        modelBuilder.Entity<Booking>()
            .HasKey(b => b.Id);

        modelBuilder.Entity<Booking>()
            .Property(b => b.CreatedAt)
            .IsRequired();

        modelBuilder.Entity<Booking>()
            .Property(b => b.UserId)
            .IsRequired();

        modelBuilder.Entity<Booking>()
            .Property(b => b.SeatId)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<Event>()
            .Property(p => p.Id)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.Name)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.Venue)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.EventDate)
            .IsRequired();

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

        modelBuilder.Entity<User>()
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        modelBuilder.Entity<Seat>()
            .HasOne(p => p.Booking)
            .WithOne(b => b.Seat)
            .HasForeignKey<Booking>(s => s.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
    }
}
