using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Domain.Background;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options, 
    IDomainEventDispatcher domainEventDispatcher) 
    : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FailedBackgroundJob> FailedBackgroundJobs => Set<FailedBackgroundJob>();
    public DbSet<ProcessedBackgroundJob> ProcessedBackgroundJobs => Set<ProcessedBackgroundJob>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithDomainEvents = GetEntitiesWithDomainEvents();
        var domainEvents = entitiesWithDomainEvents.SelectMany(entity => entity.DomainEvents).ToList();
        var saveChangesResult = await base.SaveChangesAsync(cancellationToken);

        entitiesWithDomainEvents.ForEach(e => e.ClearDomainEvents());

        if(domainEvents.Count > 0)
            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

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
