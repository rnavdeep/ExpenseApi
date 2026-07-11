using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_with_role_then_login_sets_jwt_cookie()
    {
        var user = await RegisterAndLoginAsync("alice");

        // The login handler appends the jwtToken cookie; the client kept it, so a protected
        // endpoint now succeeds.
        var summary = await user.Client.GetAsync("/api/Expense/summary?period=month");
        summary.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_without_role_succeeds()
    {
        var client = Factory.CreateClient();
        var userName = Unique("norole");

        var res = await client.PostAsJsonAsync("/api/Auth/Register", new RegisterRequestDto
        {
            Email = $"{userName}@test.local",
            UserName = userName,
            Password = "Passw0rd!",
            Roles = Array.Empty<string>()
        });

        // The frontend's registration form never sends roles, so role assignment
        // must be optional and can't gate whether the account is created.
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginRes = await client.PostAsJsonAsync("/api/Auth/Login", new LoginRequestDto
        {
            Username = userName,
            Password = "Passw0rd!"
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_duplicate_email_is_rejected()
    {
        var user = await RegisterAndLoginAsync("dup");
        var client = Factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/Auth/Register", new RegisterRequestDto
        {
            Email = user.Email,
            UserName = Unique("dup2"),
            Password = "Passw0rd!",
            Roles = new[] { "Writer" }
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_with_wrong_password_fails()
    {
        var user = await RegisterAndLoginAsync("wrongpw");
        var client = Factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/Auth/Login", new LoginRequestDto
        {
            Username = user.UserName,
            Password = "Nope123!"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        body!.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task CheckSession_reflects_authentication_state()
    {
        var anon = Factory.CreateClient();
        var anonSession = await anon.GetFromJsonAsync<SessionDataDto>("/api/Auth/checkSession");
        anonSession!.IsLoggedIn.Should().BeFalse();

        var user = await RegisterAndLoginAsync("sess");
        var authed = await user.Client.GetFromJsonAsync<SessionDataDto>("/api/Auth/checkSession");
        authed!.IsLoggedIn.Should().BeTrue();
        authed.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Protected_endpoint_requires_authentication()
    {
        var anon = Factory.CreateClient();
        var res = await anon.GetAsync("/api/Expense/balances");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
