using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

public class FriendsNotificationTests : IntegrationTestBase
{
    public FriendsNotificationTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Friends_search_finds_user_by_email()
    {
        var user = await RegisterAndLoginAsync("friend");
        var anon = Factory.CreateClient(); // NOTE: FriendsController has no [Authorize] — anonymous lookup works.

        var res = await anon.GetAsync($"/api/Friends/{user.Email}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<UserDto>();
        dto!.Username.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Friends_search_returns_not_found_for_unknown_user()
    {
        var res = await Factory.CreateClient().GetAsync($"/api/Friends/{Unique("ghost")}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Notifications_lists_the_current_users_notifications()
    {
        var user = await RegisterAndLoginAsync("notif");
        await WithDbAsync(async db =>
        {
            db.Notification.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Your receipt was processed",
                Title = "Receipt ready",
                IsRead = 0,
                IsFriendRequest = 0,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var res = await user.Client.GetAsync("/api/Notification");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await res.Content.ReadFromJsonAsync<List<JsonElement>>();
        items!.Should().HaveCountGreaterThanOrEqualTo(1);

        var readAll = await user.Client.PostAsync("/api/Notification/readAll", null);
        readAll.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Notifications_anonymous_is_not_properly_guarded()
    {
        // SECURITY GAP: NotificationController has no [Authorize]; anonymous access is not rejected
        // with 401 but instead surfaces as a 500 from the repository throwing "User not found".
        var res = await Factory.CreateClient().GetAsync("/api/Notification");
        res.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
