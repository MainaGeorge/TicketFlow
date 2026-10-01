using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Application.Authorization;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.MigrateAsync();
    }

    public static async Task SeedRoles(this IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await SeedRolesAsync(scope.ServiceProvider);
    }

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRoleExistsAsync(roleManager, Roles.User);
        await EnsureRoleExistsAsync(roleManager, Roles.Admin);
    }

    public static async Task SeedAdmin(this IHost app, IConfiguration configuration)
    {
        var adminEmail = configuration.GetValue<string>("Admin:Email");
        var adminPassword = configuration.GetValue<string>("Admin:Password");

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
            throw new InvalidOperationException($"Failed to retrieve default admin user details from configuration");

        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        adminUser ??= await CreateAdminUser(userManager, adminEmail, adminPassword);

        await AddRoleToUser(userManager, adminUser, Roles.Admin);
        await AddRoleToUser(userManager, adminUser, Roles.User);
    }

    private static async Task<User> CreateAdminUser(UserManager<User> userManager, string email, string password)
    {
        var user = new User { Email = email, DisplayName = "AdminUser", UserName = email };
        var adminUserCreationResult = await userManager.CreateAsync(user, password);

        if (!adminUserCreationResult.Succeeded)
        {
            var errors = string.Join(", ", adminUserCreationResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create default admin user: {errors}");
        }

        return user;
    }

    private static async Task AddRoleToUser(UserManager<User> userManager, User user, string role)
    {
        if (await userManager.IsInRoleAsync(user, role))
            return;

        var roleAdditionResult = await userManager.AddToRoleAsync(user, role);

        if (roleAdditionResult.Succeeded)
            return;

        var errors = string.Join(", ", roleAdditionResult.Errors.Select(error => error.Description));

        throw new InvalidOperationException($"Failed to add default admin user to role '{role}': {errors}");
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;

        var result = await roleManager.CreateAsync(new IdentityRole(role));

        if (result.Succeeded)
            return;

        var errors = string.Join(",", result.Errors.Select(error => error.Description));

        throw new InvalidOperationException($"Failed to create identity role '{role}': {errors}");
    }
}
