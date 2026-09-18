using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Services;
using TicketFlow.Application.Common.Behaviours;

namespace TicketFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
        });

        return services;
    }
}
