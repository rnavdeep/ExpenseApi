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
}
