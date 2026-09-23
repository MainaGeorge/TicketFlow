using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Background;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Messaging;
using TicketFlow.Application.Seats.Interfaces;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Background;
using TicketFlow.Infrastructure.Messaging.Consumers;
using TicketFlow.Infrastructure.Messaging.Publishers;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;
using TicketFlow.Infrastructure.Persistence.Repositories;
using TicketFlow.Infrastructure.Services;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorization(configuration);
        services.AddRepositoriesAndServices();
        services.AddScoped<IBookingConfirmationProcessor, BookingConfirmationProcessor>();
        services.AddScoped<IProcessedJobStore, ProcessedJobStore>();
        services.AddOutbox(configuration);
        services.AddMassTransit(configuration);
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }

    private static IServiceCollection AddRepositoriesAndServices(this IServiceCollection services)
    {
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventsRepository, EventRepository>();
        services.AddScoped<ISeatsRepository, SeatsRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
    
    private static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .Validate(options => options.MaxRetryAttempts > 0, "Outbox MaxRetryAttempts must be greater than 0.")
            .Validate(options => options.PollingIntervalSeconds > 0, "Outbox PollingIntervalSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }

    private static IServiceCollection AddMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<BookingConfirmationConsumer>();
            configurator.UsingRabbitMq((context, rabbitMq) =>
            {
                rabbitMq.Host(
                    configuration["RabbitMq:Host"]!,
                    "/",
                    host =>
                    {
                        host.Username(configuration["RabbitMq:Username"]!);
                        host.Password(configuration["RabbitMq:Password"]!);
                    });

                rabbitMq.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static IServiceCollection AddAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddDbContext<AppDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")))
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserManager<UserManager<User>>()
            .AddRoleManager<RoleManager<IdentityRole>>();

        return services;
    }
}