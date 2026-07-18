using System.Net.Http.Json;
using System.Security.Claims;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Expense;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// Runs an action against a scoped IExpenseRepository as if `caller` were the authenticated user
    /// (NameIdentifier claim = caller's username, the same claim repository methods resolve the current
    /// user from). For repository methods with no HTTP endpoint yet to drive them through.
    /// </summary>
    protected async Task<T> WithExpenseRepositoryAsync<T>(TestUser caller, Func<IExpenseRepository, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, caller.UserName)
            }))
        };

        var repository = scope.ServiceProvider.GetRequiredService<IExpenseRepository>();
        return await action(repository);
    }

    /// <summary>Sends a friend request from sender to recipient through the real API and accepts it.</summary>
    protected async Task BefriendAsync(TestUser sender, TestUser recipient)
    {
        var sendRequest = await sender.Client.PostAsJsonAsync("/api/Friends/sendRequest", new UserDto
        {
            Id = recipient.Id,
            Username = recipient.UserName
        });
        sendRequest.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        Guid notificationId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var friendRequest = await db.FriendRequests.FirstOrDefaultAsync(
                fr => fr.SentByUserId == sender.Id && fr.SentToUserId == recipient.Id);
            friendRequest.Should().NotBeNull();
            notificationId = friendRequest!.NotificationId;
        });

        var acceptRequest = await recipient.Client.PostAsJsonAsync("/api/Friends/acceptRequest", notificationId.ToString());
        acceptRequest.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }
}
