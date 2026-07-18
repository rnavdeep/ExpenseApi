using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// End-to-end (route -> repository -> DB) coverage for the phase B10 line-item assignment endpoints,
/// the N-of-M fields on getAssignedUsers, and the guard that retires addUser/removeUser/updateShares
/// once an expense has any LineItem row.
/// </summary>
public class ExpenseControllerLineItemTests : IntegrationTestBase
{
    public ExpenseControllerLineItemTests(CustomWebAppFactory factory) : base(factory) { }

    private const string GuardMessage =
        "This expense's sharing is managed by line-item assignment — use the Document Results page instead.";

    [Fact]
    public async Task PutLineItemAssignee_with_a_non_friend_returns_400_with_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("eclowner");
        var stranger = await RegisterAndLoginAsync("eclstranger");

        Guid itemId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Dinner", 10m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 10m);
            var item = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Pasta", 10m, owner.Id);
            await db.SaveChangesAsync();
            itemId = item.Id;
        });

        var res = await owner.Client.PutAsync($"/api/Expense/lineItem/{itemId}/assignees/{stranger.Id}", null);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Users must be friends before sharing an expense.");
    }

    [Fact]
    public async Task PutLineItemAssignee_with_an_accepted_friend_returns_200_with_updated_assignees()
    {
        var owner = await RegisterAndLoginAsync("eclowner2");
        var friend = await RegisterAndLoginAsync("eclfriend2");
        await BefriendAsync(owner, friend);

        Guid itemId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Dinner", 10m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 10m);
            var item = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Pasta", 10m, owner.Id);
            await db.SaveChangesAsync();
            itemId = item.Id;
        });

        var res = await owner.Client.PutAsync($"/api/Expense/lineItem/{itemId}/assignees/{friend.Id}", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var dto = await res.Content.ReadFromJsonAsync<LineItemDto>();
        dto!.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id, friend.Id });
    }

    [Fact]
    public async Task DeleteLineItemAssignee_last_remaining_assignee_returns_400_with_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("eclowner3");

        Guid itemId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Snack", 10m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 10m);
            var item = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Chips", 10m, owner.Id);
            await db.SaveChangesAsync();
            itemId = item.Id;
        });

        var res = await owner.Client.DeleteAsync($"/api/Expense/lineItem/{itemId}/assignees/{owner.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Cannot remove the last remaining assignee from a line item.");
    }

    [Fact]
    public async Task DeleteLineItemAssignee_with_more_than_one_assignee_returns_200_with_updated_assignees()
    {
        var owner = await RegisterAndLoginAsync("eclowner4");
        var friend = await RegisterAndLoginAsync("eclfriend4");
        await BefriendAsync(owner, friend);

        Guid itemId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Lunch", 10m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 10m);
            var item = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Sandwich", 10m, owner.Id, friend.Id);
            await db.SaveChangesAsync();
            itemId = item.Id;
        });

        var res = await owner.Client.DeleteAsync($"/api/Expense/lineItem/{itemId}/assignees/{friend.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var dto = await res.Content.ReadFromJsonAsync<LineItemDto>();
        dto!.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id });
    }

    [Fact]
    public async Task PutAssignAllLineItems_is_additive_and_rejects_non_friends()
    {
        var owner = await RegisterAndLoginAsync("eclowner5");
        var friendA = await RegisterAndLoginAsync("eclfrienda5");
        var friendB = await RegisterAndLoginAsync("eclfriendb5");
        var stranger = await RegisterAndLoginAsync("eclstranger5");
        await BefriendAsync(owner, friendA);
        await BefriendAsync(owner, friendB);

        Guid expenseId = Guid.Empty, item1Id = Guid.Empty, item2Id = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Groceries", 40m, "Groceries", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 40m);
            var item1 = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Milk", 20m, owner.Id, friendA.Id);
            var item2 = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Bread", 20m, owner.Id);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
            item1Id = item1.Id;
            item2Id = item2.Id;
        });

        var nonFriendRes = await owner.Client.PutAsync($"/api/Expense/{expenseId}/lineItems/assignAll/{stranger.Id}", null);
        nonFriendRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await nonFriendRes.Content.ReadAsStringAsync()).Should().Contain("Users must be friends before sharing an expense.");

        var res = await owner.Client.PutAsync($"/api/Expense/{expenseId}/lineItems/assignAll/{friendB.Id}", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var items = await res.Content.ReadFromJsonAsync<List<LineItemDto>>();
        items!.Single(i => i.Id == item1Id).Assignees.Select(a => a.UserId)
            .Should().BeEquivalentTo(new[] { owner.Id, friendA.Id, friendB.Id });
        items.Single(i => i.Id == item2Id).Assignees.Select(a => a.UserId)
            .Should().BeEquivalentTo(new[] { owner.Id, friendB.Id });
    }

    [Fact]
    public async Task GetAssignedUsers_reports_N_of_M_line_item_counts_when_the_expense_has_line_items()
    {
        var owner = await RegisterAndLoginAsync("eclowner6");
        var friend = await RegisterAndLoginAsync("eclfriend6");
        await BefriendAsync(owner, friend);

        Guid expenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Groceries", 40m, "Groceries", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 40m);
            SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Milk", 20m, owner.Id, friend.Id);
            SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Bread", 20m, owner.Id);
            SeedData.AddShare(db, expense.Id, owner.Id, 30, 0.75);
            SeedData.AddShare(db, expense.Id, friend.Id, 10, 0.25);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
        });

        var res = await owner.Client.GetAsync($"/api/Expense/{expenseId}/getAssignedUsers");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await res.Content.ReadFromJsonAsync<List<ExpenseUserDto>>();
        users!.Should().HaveCount(2);
        users.First(u => u.UserId == owner.Id).ItemsAssignedCount.Should().Be(2);
        users.First(u => u.UserId == owner.Id).TotalItemsCount.Should().Be(2);
        users.First(u => u.UserId == friend.Id).ItemsAssignedCount.Should().Be(1);
        users.First(u => u.UserId == friend.Id).TotalItemsCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAssignedUsers_leaves_the_N_of_M_fields_null_when_the_expense_has_no_line_items()
    {
        var owner = await RegisterAndLoginAsync("eclowner7");
        var friend = await RegisterAndLoginAsync("eclfriend7");
        await BefriendAsync(owner, friend);

        var created = await owner.Client.PostAsJsonAsync("/api/Expense", new AddExpenseDto { Title = "Dinner", Description = "Shared dinner" });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var expense = await created.Content.ReadFromJsonAsync<ExpenseDto>();

        var add = await owner.Client.PostAsync($"/api/Expense/{expense!.Id}/addUser?userId={friend.Id}", null);
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await owner.Client.GetAsync($"/api/Expense/{expense.Id}/getAssignedUsers");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await res.Content.ReadFromJsonAsync<List<ExpenseUserDto>>();
        users!.Should().OnlyContain(u => u.ItemsAssignedCount == null && u.TotalItemsCount == null);
    }

    [Fact]
    public async Task AddUser_removeUser_and_updateShares_are_guarded_once_the_expense_has_a_line_item()
    {
        var owner = await RegisterAndLoginAsync("eclowner8");
        var friend = await RegisterAndLoginAsync("eclfriend8");
        await BefriendAsync(owner, friend);

        Guid expenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Dinner", 10m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 10m);
            SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Pasta", 10m, owner.Id);
            SeedData.AddShare(db, expense.Id, owner.Id, 10, 1.0);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
        });

        var addRes = await owner.Client.PostAsync($"/api/Expense/{expenseId}/addUser?userId={friend.Id}", null);
        addRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await addRes.Content.ReadAsStringAsync()).Should().Contain(GuardMessage);

        var removeRes = await owner.Client.DeleteAsync($"/api/Expense/{expenseId}/removeUser/{owner.Id}");
        removeRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await removeRes.Content.ReadAsStringAsync()).Should().Contain(GuardMessage);

        var sharesRes = await owner.Client.PutAsJsonAsync($"/api/Expense/{expenseId}/updateShares",
            new List<UpdateExpenseUserShareDto> { new UpdateExpenseUserShareDto { UserId = owner.Id, UserShare = 1.0 } });
        sharesRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sharesRes.Content.ReadAsStringAsync()).Should().Contain(GuardMessage);
    }
}
