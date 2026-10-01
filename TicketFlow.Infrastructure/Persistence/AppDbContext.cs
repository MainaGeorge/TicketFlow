using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Background;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence.Outbox;

namespace TicketFlow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User, IdentityRole, string>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FailedBackgroundJob> FailedBackgroundJobs => Set<FailedBackgroundJob>();
    public DbSet<ProcessedBackgroundJob> ProcessedBackgroundJobs => Set<ProcessedBackgroundJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithDomainEvents = GetEntitiesWithDomainEvents();
        var outboxMessages = entitiesWithDomainEvents.SelectMany(entity => entity.DomainEvents).Select(OutboxMessageFactory.CreateDomainEvent).ToList();
        
        OutboxMessages.AddRange(outboxMessages);
        var saveChangesResult = await base.SaveChangesAsync(cancellationToken);

        entitiesWithDomainEvents.ForEach(e => e.ClearDomainEvents());
        return saveChangesResult;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private List<IHasDomainEvents> GetEntitiesWithDomainEvents()
    {
        return ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();
    }
}
