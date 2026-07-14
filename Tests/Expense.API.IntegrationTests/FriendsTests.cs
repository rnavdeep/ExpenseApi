using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

public class FriendsTests : IntegrationTestBase
{
    public FriendsTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task GetFriends_returns_the_counterparty_userId_when_caller_sent_the_request()
    {
        var sender = await RegisterAndLoginAsync("sender");
        var recipient = await RegisterAndLoginAsync("recipient");
        await BefriendAsync(sender, recipient);

        var res = await sender.Client.GetAsync("/api/Friends/getFriends");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await res.Content.ReadFromJsonAsync<List<FriendsListDto>>();

        friends.Should().ContainSingle(f => f.Username == recipient.UserName)
            .Which.UserId.Should().Be(recipient.Id);
    }

    [Fact]
    public async Task GetFriends_returns_the_counterparty_userId_when_caller_received_the_request()
    {
        var sender = await RegisterAndLoginAsync("sender");
        var recipient = await RegisterAndLoginAsync("recipient");
        await BefriendAsync(sender, recipient);

        var res = await recipient.Client.GetAsync("/api/Friends/getFriends");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await res.Content.ReadFromJsonAsync<List<FriendsListDto>>();

        friends.Should().ContainSingle(f => f.Username == sender.UserName)
            .Which.UserId.Should().Be(sender.Id);
    }

    [Fact]
    public async Task GetFriends_reports_the_actual_count_of_expenses_shared_with_that_friend()
    {
        var user = await RegisterAndLoginAsync("user");
        var friend = await RegisterAndLoginAsync("friend");
        var stranger = await RegisterAndLoginAsync("stranger");
        await BefriendAsync(user, friend);

        await WithDbAsync(async db =>
        {
            var shared1 = SeedData.AddExpense(db, user.Id, "Dinner", 40m, "Dining", DateTime.UtcNow);
            SeedData.AddShare(db, shared1.Id, friend.Id, 20);

            var shared2 = SeedData.AddExpense(db, friend.Id, "Groceries", 60m, "Groceries", DateTime.UtcNow);
            SeedData.AddShare(db, shared2.Id, user.Id, 30);

            // Not shared with `friend` - should not be counted.
            var soloExpense = SeedData.AddExpense(db, user.Id, "Solo", 15m, "Misc", DateTime.UtcNow);
            SeedData.AddShare(db, soloExpense.Id, stranger.Id, 7.5);

            await db.SaveChangesAsync();
        });

        var res = await user.Client.GetAsync("/api/Friends/getFriends");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await res.Content.ReadFromJsonAsync<List<FriendsListDto>>();

        friends.Should().ContainSingle(f => f.Username == friend.UserName)
            .Which.SharedExpenses.Should().HaveCount(2);
    }
}
