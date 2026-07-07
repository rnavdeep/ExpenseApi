using System.Net.Http.Json;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Expense.API.IntegrationTests.Infrastructure;

/// <summary>A user that has been registered + logged in; the client carries its jwtToken cookie.</summary>
public record TestUser(Guid Id, string UserName, string Email, string Password, HttpClient Client);

[Collection("Api")]
public abstract class IntegrationTestBase
{
    protected readonly CustomWebAppFactory Factory;

    protected IntegrationTestBase(CustomWebAppFactory factory) => Factory = factory;

    protected static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 9);

    /// <summary>Runs an action against a scoped business DbContext.</summary>
    protected async Task WithDbAsync(Func<UserDocumentsDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDocumentsDbContext>();
        await action(db);
    }

    /// <summary>
    /// Registers a user through the real API (which also creates the business Users row, required
    /// because a role is supplied) and logs in, returning a client whose cookie is authenticated.
    /// </summary>
    protected async Task<TestUser> RegisterAndLoginAsync(string? namePrefix = null)
    {
        var userName = Unique(namePrefix ?? "user");
        var email = $"{userName}@test.local";
        const string password = "Passw0rd!";
        var client = Factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/Auth/Register", new RegisterRequestDto
        {
            Email = email,
            UserName = userName,
            Password = password,
            Roles = new[] { "Writer" }
        });
        register.IsSuccessStatusCode.Should().BeTrue(
            $"register should succeed but returned {(int)register.StatusCode}: {await register.Content.ReadAsStringAsync()}");

        var login = await client.PostAsJsonAsync("/api/Auth/Login", new LoginRequestDto
        {
            Username = userName,
            Password = password
        });
        login.IsSuccessStatusCode.Should().BeTrue(
            $"login should succeed but returned {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");

        Guid id = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == userName);
            user.Should().NotBeNull("registration must create a matching business Users row");
            id = user!.Id;
        });

        return new TestUser(id, userName, email, password, client);
    }

    /// <summary>Inserts a business-DB user directly (a counterparty who never needs to log in).</summary>
    protected async Task<User> CreateBusinessUserAsync(string namePrefix = "peer")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = Unique(namePrefix),
            Email = $"{Unique(namePrefix)}@test.local",
            CreatedAt = DateTime.UtcNow
        };
        await WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return user;
    }
}
