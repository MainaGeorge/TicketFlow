using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TicketFlow.Tests;

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

    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}