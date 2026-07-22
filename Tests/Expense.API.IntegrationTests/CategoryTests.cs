using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Expense.API.IntegrationTests;

public class CategoryTests : IntegrationTestBase
{
    public CategoryTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Put_creates_then_updates_the_same_row()
    {
        var alice = await RegisterAndLoginAsync("categoryalice");

        var createRes = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 200m
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createRes.Content.ReadFromJsonAsync<CategoryDto>();
        created!.Name.Should().Be("Groceries");
        created.MonthlyLimit.Should().Be(200m);

        var updateRes = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 300m
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<CategoryDto>();
        updated!.Id.Should().Be(created.Id);
        updated.MonthlyLimit.Should().Be(300m);

        await WithDbAsync(async db =>
        {
            var rows = await db.Categories.Where(c => c.UserId == alice.Id).ToListAsync();
            rows.Should().HaveCount(1);
            rows[0].MonthlyLimit.Should().Be(300m);
        });
    }

    [Fact]
    public async Task Put_rejects_non_positive_limit()
    {
        var alice = await RegisterAndLoginAsync("categoryzero");

        var res = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 0m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_rejects_blank_name()
    {
        var alice = await RegisterAndLoginAsync("categoryblank");

        var res = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "   ",
            MonthlyLimit = 100m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_returns_status_with_exact_spend_including_other_bucket()
    {
        var alice = await RegisterAndLoginAsync("categorystatus");
        var now = DateTime.UtcNow;
        var lastMonth = now.AddMonths(-1);

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Groceries", MonthlyLimit = 300m });
        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Other", MonthlyLimit = 50m });

        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Weekly shop", 120m, "Groceries", now);
            SeedData.AddExpense(db, alice.Id, "More shopping", 40m, "Groceries", now);
            SeedData.AddExpense(db, alice.Id, "Misc", 25m, null, now);
            SeedData.AddExpense(db, alice.Id, "Prior month shop", 999m, "Groceries", lastMonth);
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Category?period=month");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await res.Content.ReadFromJsonAsync<List<CategoryStatusDto>>();

        statuses.Should().HaveCount(2);
        statuses.Should().ContainSingle(s => s.Name == "Groceries" && s.MonthlyLimit == 300m && s.Spent == 160m);
        statuses.Should().ContainSingle(s => s.Name == "Other" && s.MonthlyLimit == 50m && s.Spent == 25m);
    }

    [Fact]
    public async Task Get_returns_not_found_when_caller_has_no_categories()
    {
        var lonely = await RegisterAndLoginAsync("categorylonely");

        var res = await lonely.Client.GetAsync("/api/Category");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var getRes = await Factory.CreateClient().GetAsync("/api/Category");
        getRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var putRes = await Factory.CreateClient().PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 100m
        });
        putRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var deleteRes = await Factory.CreateClient().DeleteAsync($"/api/Category/{Guid.NewGuid()}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var expensesRes = await Factory.CreateClient().GetAsync("/api/Category/Groceries/expenses");
        expensesRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_removes_the_category_and_returns_no_content()
    {
        var alice = await RegisterAndLoginAsync("categorydeleteok");

        var createRes = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 200m
        });
        var created = await createRes.Content.ReadFromJsonAsync<CategoryDto>();

        var deleteRes = await alice.Client.DeleteAsync($"/api/Category/{created!.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await WithDbAsync(async db =>
        {
            var rows = await db.Categories.Where(c => c.UserId == alice.Id).ToListAsync();
            rows.Should().BeEmpty();
        });

        var getRes = await alice.Client.GetAsync("/api/Category");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_returns_not_found_for_nonexistent_id()
    {
        var alice = await RegisterAndLoginAsync("categorydeletemissing");

        var res = await alice.Client.DeleteAsync($"/api/Category/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_rejects_another_users_category()
    {
        var alice = await RegisterAndLoginAsync("categorydeletealice");
        var bob = await RegisterAndLoginAsync("categorydeletebob");

        var createRes = await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto
        {
            Name = "Groceries",
            MonthlyLimit = 200m
        });
        var created = await createRes.Content.ReadFromJsonAsync<CategoryDto>();

        var deleteRes = await bob.Client.DeleteAsync($"/api/Category/{created!.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await WithDbAsync(async db =>
        {
            var rows = await db.Categories.Where(c => c.UserId == alice.Id).ToListAsync();
            rows.Should().HaveCount(1);
        });
    }

    [Fact]
    public async Task GetCategoryExpenses_returns_recent_expenses_for_the_category_capped_and_ordered()
    {
        var alice = await RegisterAndLoginAsync("categorypreviewordr");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            for (var i = 0; i < 6; i++)
            {
                SeedData.AddExpense(db, alice.Id, $"Dinner {i}", 10m + i, "Dining", now.AddMinutes(-i));
            }
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Category/Dining/expenses");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var expenses = await res.Content.ReadFromJsonAsync<List<CategoryExpenseDto>>();

        expenses.Should().HaveCount(5);
        expenses.Should().BeInDescendingOrder(e => e.CreatedAt);
        expenses![0].Title.Should().Be("Dinner 0");
    }

    [Fact]
    public async Task GetCategoryExpenses_normalizes_other_bucket_like_status_does()
    {
        var alice = await RegisterAndLoginAsync("categorypreviewother");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Misc", 25m, null, now);
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Category/Other/expenses");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var expenses = await res.Content.ReadFromJsonAsync<List<CategoryExpenseDto>>();

        expenses.Should().ContainSingle(e => e.Title == "Misc");
    }

    [Fact]
    public async Task GetCategoryExpenses_excludes_other_users_expenses()
    {
        var alice = await RegisterAndLoginAsync("categorypreviewalice");
        var bob = await RegisterAndLoginAsync("categorypreviewbob");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, bob.Id, "Bob's dinner", 40m, "Dining", now);
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Category/Dining/expenses");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var expenses = await res.Content.ReadFromJsonAsync<List<CategoryExpenseDto>>();

        expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoryExpenses_returns_empty_array_when_none_this_month()
    {
        var alice = await RegisterAndLoginAsync("categorypreviewempty");

        var res = await alice.Client.GetAsync("/api/Category/Dining/expenses");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var expenses = await res.Content.ReadFromJsonAsync<List<CategoryExpenseDto>>();
        expenses.Should().BeEmpty();
    }
}
