using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Abstractions.Caching;
using TicketFlow.Application.Abstractions.Messaging;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Background;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Background;
using TicketFlow.Infrastructure.Caching;
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
        services.AddRedis(configuration);

        return services;
    }

    private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions =StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            options.ConfigurationOptions.ConnectTimeout = 1000;
            options.ConfigurationOptions.SyncTimeout = 1000;
            options.ConfigurationOptions.AsyncTimeout = 1000;
        });

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
            .Validate(options => options.BatchSize > 0, "Outbox BatchSize must be greater than 0.")
            .ValidateOnStart();

        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }

    private static IServiceCollection AddMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<BookingConfirmationConsumer>(consumer =>
            {
                consumer.UseMessageRetry(retry =>
                {
                    retry.Interval(retryCount: 3, interval: TimeSpan.FromSeconds(2));
                });
            });

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