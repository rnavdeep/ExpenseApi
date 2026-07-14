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

    private static async Task<ExpenseDto> CreateExpenseAsync(HttpClient client, string title, string description,
        decimal amount = 0, bool allowReceipts = true)
    {
        var res = await client.PostAsJsonAsync("/api/Expense", new AddExpenseDto
        {
            Title = title,
            Description = description,
            Amount = amount,
            AllowReceipts = allowReceipts
        });
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

    [Fact]
    public async Task Create_persists_manual_amount_as_starting_value()
    {
        var user = await RegisterAndLoginAsync("amt");

        var created = await CreateExpenseAsync(user.Client, "Groceries", "Weekly shop", amount: 50m);

        created.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task Create_without_receipts_blocks_document_upload()
    {
        var user = await RegisterAndLoginAsync("norcpt");
        var created = await CreateExpenseAsync(user.Client, "Cash tip", "No receipt", amount: 20m, allowReceipts: false);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "receipt.png");

        var res = await user.Client.PostAsync($"/api/Expense/{created.Id}/uploadDoc", content);

        res.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Update_amount_below_scanned_receipts_floor_is_rejected()
    {
        var user = await RegisterAndLoginAsync("floorlow");
        var created = await CreateExpenseAsync(user.Client, "Dinner", "Team dinner", amount: 200m);

        await WithDbAsync(async db =>
        {
            var doc = SeedData.AddReceipt(db, user.Id, Guid.Parse(created.Id), DateTime.UtcNow);
            SeedData.AddDocumentJobResult(db, user.Id, Guid.Parse(created.Id), doc.Id, 100m);
            await db.SaveChangesAsync();
        });

        var res = await user.Client.PutAsJsonAsync($"/api/Expense/{created.Id}",
            new UpdateExpenseDto(created.Id, "Dinner", "Team dinner", amount: 50m));

        res.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var fetched = await user.Client.GetFromJsonAsync<ExpenseDto>($"/api/Expense/{created.Id}");
        fetched!.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Update_amount_at_or_above_floor_succeeds()
    {
        var user = await RegisterAndLoginAsync("floorok");
        var created = await CreateExpenseAsync(user.Client, "Dinner", "Team dinner", amount: 200m);

        await WithDbAsync(async db =>
        {
            var doc = SeedData.AddReceipt(db, user.Id, Guid.Parse(created.Id), DateTime.UtcNow);
            SeedData.AddDocumentJobResult(db, user.Id, Guid.Parse(created.Id), doc.Id, 100m);
            await db.SaveChangesAsync();
        });

        var res = await user.Client.PutAsJsonAsync($"/api/Expense/{created.Id}",
            new UpdateExpenseDto(created.Id, "Dinner", "Team dinner", amount: 150m));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<ExpenseDto>();
        updated!.Amount.Should().Be(150m);
    }

    [Fact]
    public async Task Update_without_amount_leaves_existing_amount_untouched()
    {
        var user = await RegisterAndLoginAsync("noamt");
        var created = await CreateExpenseAsync(user.Client, "Old", "Old desc", amount: 42m);

        var res = await user.Client.PutAsJsonAsync($"/api/Expense/{created.Id}",
            new UpdateExpenseDto(created.Id, "New", "New desc"));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<ExpenseDto>();
        updated!.Amount.Should().Be(42m);
    }
}
