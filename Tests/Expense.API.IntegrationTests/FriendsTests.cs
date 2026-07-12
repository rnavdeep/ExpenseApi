using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Expense.API.IntegrationTests;

public class FriendsTests : IntegrationTestBase
{
    public FriendsTests(CustomWebAppFactory factory) : base(factory) { }

    private async Task BefriendAsync(TestUser sender, TestUser recipient)
    {
        var sendRequest = await sender.Client.PostAsJsonAsync("/api/Friends/sendRequest", new UserDto
        {
            Id = recipient.Id,
            Username = recipient.UserName
        });
        sendRequest.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid notificationId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var friendRequest = await db.FriendRequests.FirstOrDefaultAsync(
                fr => fr.SentByUserId == sender.Id && fr.SentToUserId == recipient.Id);
            friendRequest.Should().NotBeNull();
            notificationId = friendRequest!.NotificationId;
        });

        var acceptRequest = await recipient.Client.PostAsJsonAsync("/api/Friends/acceptRequest", notificationId.ToString());
        acceptRequest.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

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
