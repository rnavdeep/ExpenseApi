using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Expense.API.IntegrationTests;

public class BudgetTests : IntegrationTestBase
{
    public BudgetTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Put_creates_then_updates_the_same_row()
    {
        var alice = await RegisterAndLoginAsync("budgetalice");

        var createRes = await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto
        {
            Category = "Groceries",
            MonthlyLimit = 200m
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createRes.Content.ReadFromJsonAsync<BudgetDto>();
        created!.Category.Should().Be("Groceries");
        created.MonthlyLimit.Should().Be(200m);

        var updateRes = await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto
        {
            Category = "Groceries",
            MonthlyLimit = 300m
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<BudgetDto>();
        updated!.Id.Should().Be(created.Id);
        updated.MonthlyLimit.Should().Be(300m);

        await WithDbAsync(async db =>
        {
            var rows = await db.Budgets.Where(b => b.UserId == alice.Id).ToListAsync();
            rows.Should().HaveCount(1);
            rows[0].MonthlyLimit.Should().Be(300m);
        });
    }

    [Fact]
    public async Task Put_rejects_non_positive_limit()
    {
        var alice = await RegisterAndLoginAsync("budgetzero");

        var res = await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto
        {
            Category = "Groceries",
            MonthlyLimit = 0m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_rejects_blank_category()
    {
        var alice = await RegisterAndLoginAsync("budgetblank");

        var res = await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto
        {
            Category = "   ",
            MonthlyLimit = 100m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_returns_status_with_exact_spend_including_other_bucket()
    {
        var alice = await RegisterAndLoginAsync("budgetstatus");
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);

        await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto { Category = "Groceries", MonthlyLimit = 300m });
        await alice.Client.PutAsJsonAsync("/api/Budget", new UpsertBudgetDto { Category = "Other", MonthlyLimit = 50m });

        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Weekly shop", 120m, "Groceries", thisMonth);
            SeedData.AddExpense(db, alice.Id, "More shopping", 40m, "Groceries", thisMonth);
            SeedData.AddExpense(db, alice.Id, "Misc", 25m, null, thisMonth);
            SeedData.AddExpense(db, alice.Id, "Prior month shop", 999m, "Groceries", lastMonth);
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Budget?period=month");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await res.Content.ReadFromJsonAsync<List<BudgetStatusDto>>();

        statuses.Should().HaveCount(2);
        statuses.Should().ContainSingle(s => s.Category == "Groceries" && s.MonthlyLimit == 300m && s.Spent == 160m);
        statuses.Should().ContainSingle(s => s.Category == "Other" && s.MonthlyLimit == 50m && s.Spent == 25m);
    }

    [Fact]
    public async Task Get_returns_not_found_when_caller_has_no_budgets()
    {
        var lonely = await RegisterAndLoginAsync("budgetlonely");

        var res = await lonely.Client.GetAsync("/api/Budget");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var getRes = await Factory.CreateClient().GetAsync("/api/Budget");
        getRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var putRes = await Factory.CreateClient().PutAsJsonAsync("/api/Budget", new UpsertBudgetDto
        {
            Category = "Groceries",
            MonthlyLimit = 100m
        });
        putRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
