using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Services;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Services;
using TicketFlow.Application.Seats.Interfaces;
using TicketFlow.Application.Seats.Services;

namespace TicketFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEventsService, EventsService>();
        services.AddScoped<ISeatsService, SeatsService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
