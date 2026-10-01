using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TicketFlow.Application.Authorization;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Integration;

public static class TestHelpers
{
    public static async Task<string> RegisterAndLogin(
        HttpClient client,
        string email)
    {
        var registerResponse =
            await client.PostAsJsonAsync("/api/auth/register",
                new
                {
                    email,
                    password = "Password123!",
                    displayName = "Test User"
                });

        registerResponse.EnsureSuccessStatusCode();

        var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email,
                    password = "Password123!"
                });

        loginResponse.EnsureSuccessStatusCode();

        var token = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        return token?.AccessToken ?? throw new InvalidOperationException("Login did not return an access token.");
    }

    public static async Task<string> RegisterAndLoginAsAdmin(CustomWebApplicationFactory factory, HttpClient client, string email)
    {
        var registerResponse =
            await client.PostAsJsonAsync("/api/auth/register",
                new
                {
                    email,
                    password = "Password123!",
                    displayName = "Test User"
                });

        registerResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var registeredUser = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException();
        var addAdminRoleResult = await userManager.AddToRoleAsync(registeredUser, Roles.Admin);

        if (!addAdminRoleResult.Succeeded)
        {
            var errors = string.Join(", ", addAdminRoleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to assign Admin role to test user: {errors}");
        }

        var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email,
                    password = "Password123!"
                });

        loginResponse.EnsureSuccessStatusCode();

        var token = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        return token?.AccessToken ?? throw new InvalidOperationException("Login did not return an access token.");
    }

    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}