using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

public class AssignUsersTests : IntegrationTestBase
{
    public AssignUsersTests(CustomWebAppFactory factory) : base(factory) { }

    private static async Task<ExpenseDto> CreateExpenseAsync(HttpClient client, string title, string description)
    {
        var res = await client.PostAsync($"/api/Expense?title={title}&description={description}", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ExpenseDto>())!;
    }

    [Fact]
    public async Task AddUser_with_a_non_friend_returns_400_with_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("owner");
        var stranger = await RegisterAndLoginAsync("stranger");
        var created = await CreateExpenseAsync(owner.Client, "Dinner", "Shared dinner");

        var add = await owner.Client.PostAsync($"/api/Expense/{created.Id}/addUser?userId={stranger.Id}", null);

        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await add.Content.ReadAsStringAsync()).Should().Contain("Users must be friends before sharing an expense.");
    }

    [Fact]
    public async Task AddUser_with_an_accepted_friend_returns_200_and_shares_recalculate()
    {
        var owner = await RegisterAndLoginAsync("owner");
        var friend = await RegisterAndLoginAsync("friend");
        await BefriendAsync(owner, friend);
        var created = await CreateExpenseAsync(owner.Client, "Dinner", "Shared dinner");

        var add = await owner.Client.PostAsync($"/api/Expense/{created.Id}/addUser?userId={friend.Id}", null);
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await owner.Client.GetAsync($"/api/Expense/{created.Id}/getAssignedUsers");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await res.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        users!.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddUser_with_a_pending_friend_request_returns_400()
    {
        var owner = await RegisterAndLoginAsync("owner");
        var pending = await RegisterAndLoginAsync("pending");
        var created = await CreateExpenseAsync(owner.Client, "Dinner", "Shared dinner");

        var sendRequest = await owner.Client.PostAsJsonAsync("/api/Friends/sendRequest", new UserDto
        {
            Id = pending.Id,
            Username = pending.UserName
        });
        sendRequest.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var add = await owner.Client.PostAsync($"/api/Expense/{created.Id}/addUser?userId={pending.Id}", null);

        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await add.Content.ReadAsStringAsync()).Should().Contain("Users must be friends before sharing an expense.");
    }
}
