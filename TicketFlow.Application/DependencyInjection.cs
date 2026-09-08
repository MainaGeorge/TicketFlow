using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Bookings;
using TicketFlow.Application.Events;
using TicketFlow.Application.Seats;

namespace TicketFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBookingService, BookingsService>();
        services.AddScoped<IEventsService, EventsService>();
        services.AddScoped<ISeatsService, SeatsService>();
        return services;
    }
}
