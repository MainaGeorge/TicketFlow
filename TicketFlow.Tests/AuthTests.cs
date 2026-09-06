using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TicketFlow.Presentation.Data;
using TicketFlow.Presentation.Models;

namespace TicketFlow.Tests;

public class AuthTests
{
    [Fact]
    public async Task Register_WithValidRequest_Returns201()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@test.com", password = "Password123!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice.com is not an email", password = "Password123!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice.com is not an email", password = "abc", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@email.com", password = "Password12!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@email.com", password = "Password123!", displayName = "Alice" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new { email = "alice@test.com", password = "Password123!", displayName = "Alice" });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync("alice@test.com");

        user!.IsActive = false;

        await dbContext.SaveChangesAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alice@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
