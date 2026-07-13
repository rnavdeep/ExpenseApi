using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

public class ExpenseCrudTests : IntegrationTestBase
{
    public ExpenseCrudTests(CustomWebAppFactory factory) : base(factory) { }

    private sealed record ExpenseList(List<ExpenseDto> Expenses, int TotalRows);

    private static async Task<ExpenseDto> CreateExpenseAsync(HttpClient client, string title, string description)
    {
        var res = await client.PostAsync($"/api/Expense?title={title}&description={description}", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ExpenseDto>())!;
    }

    [Fact]
    public async Task Create_then_list_returns_only_the_users_expenses()
    {
        var user = await RegisterAndLoginAsync("crud");

        await CreateExpenseAsync(user.Client, "Coffee", "Morning coffee");
        await CreateExpenseAsync(user.Client, "Lunch", "Team lunch");

        var list = await user.Client.GetFromJsonAsync<ExpenseList>("/api/Expense?pageNumber=1&pageSize=50");
        list!.Expenses.Should().HaveCount(2);
        list.Expenses.Select(e => e.Title).Should().Contain(new[] { "Coffee", "Lunch" });
    }

    [Fact]
    public async Task Get_by_id_returns_the_expense()
    {
        var user = await RegisterAndLoginAsync("getid");
        var created = await CreateExpenseAsync(user.Client, "Taxi", "Airport taxi");

        var res = await user.Client.GetAsync($"/api/Expense/{created.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await res.Content.ReadFromJsonAsync<ExpenseDto>();
        fetched!.Title.Should().Be("Taxi");
    }

    [Fact]
    public async Task Update_changes_title_description_and_category()
    {
        var user = await RegisterAndLoginAsync("upd");
        var created = await CreateExpenseAsync(user.Client, "Old", "Old desc");

        var res = await user.Client.PutAsJsonAsync($"/api/Expense/{created.Id}",
            new UpdateExpenseDto(created.Id, "New", "New desc", "Travel"));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<ExpenseDto>();
        updated!.Title.Should().Be("New");
        updated.Category.Should().Be("Travel");
    }

    [Fact]
    public async Task AddUser_then_getAssignedUsers_lists_creator_and_peer()
    {
        var user = await RegisterAndLoginAsync("assign");
        var peer = await RegisterAndLoginAsync("peer");
        await BefriendAsync(user, peer);
        var created = await CreateExpenseAsync(user.Client, "Dinner", "Shared dinner");

        var add = await user.Client.PostAsync($"/api/Expense/{created.Id}/addUser?userId={peer.Id}", null);
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await user.Client.GetAsync($"/api/Expense/{created.Id}/getAssignedUsers");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await res.Content.ReadFromJsonAsync<List<JsonElement>>();
        // Creator is auto-assigned on create, plus the peer we just added.
        users!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_removes_expense_and_second_delete_is_not_found()
    {
        var user = await RegisterAndLoginAsync("del");
        // Seed a bare expense (no split rows) so the delete isn't blocked by child references.
        var expenseId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, user.Id, "Temp", 10m, "Misc", DateTime.UtcNow);
            var e = db.Expenses.Local.First();
            expenseId = e.Id;
            await db.SaveChangesAsync();
        });

        var first = await user.Client.DeleteAsync($"/api/Expense/{expenseId}");
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await user.Client.DeleteAsync($"/api/Expense/{expenseId}");
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Count_endpoint_returns_ok()
    {
        var user = await RegisterAndLoginAsync("count");
        await CreateExpenseAsync(user.Client, "One", "First");

        var res = await user.Client.GetAsync("/api/Expense/count");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalRows").GetInt32().Should().BeGreaterThan(0);
    }
}
