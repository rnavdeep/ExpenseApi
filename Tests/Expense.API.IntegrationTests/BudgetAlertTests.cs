using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// Amount/category aren't settable via the public create endpoint (POST /api/Expense hardcodes
/// amount=0 and leaves category null), so these push a category's current-month spend across a
/// budget threshold via PUT /api/Expense/{id} category reassignment - the only lever the existing
/// HTTP surface offers to move a priced, seeded expense into a budgeted category. The same
/// ExpenseRepository hook fires identically from CreateExpenseAsync.
/// </summary>
public class BudgetAlertTests : IntegrationTestBase
{
    public BudgetAlertTests(CustomWebAppFactory factory) : base(factory) { }

    private async Task<List<NotificationDto>> GetBudgetAlertsAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/Notification");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await res.Content.ReadFromJsonAsync<List<NotificationDto>>();
        return notifications!.Where(n => n.Title == "Budget alert").ToList();
    }

    [Fact]
    public async Task Category_reassignment_crossing_80_percent_yields_exactly_one_alert()
    {
        var alice = await RegisterAndLoginAsync("budgetalert80");
        var now = DateTime.UtcNow;

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Groceries", MonthlyLimit = 300m });

        var movedExpenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Groceries 1", 120m, "Groceries", now);
            SeedData.AddExpense(db, alice.Id, "Groceries 2", 90m, "Groceries", now); // 210 = 70%
            var moved = SeedData.AddExpense(db, alice.Id, "Snack run", 30m, "Snacks", now); // -> 240 = 80% once moved
            await db.SaveChangesAsync();
            movedExpenseId = moved.Id;
        });

        var res = await alice.Client.PutAsJsonAsync($"/api/Expense/{movedExpenseId}",
            new UpdateExpenseDto(movedExpenseId.ToString(), "Snack run", "Snack run description", "Groceries"));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var alerts = await GetBudgetAlertsAsync(alice.Client);
        alerts.Should().ContainSingle();
        alerts[0].Message.Should().Be("You've used 80% of your $300 Groceries budget this month");
    }

    [Fact]
    public async Task Category_reassignment_staying_below_80_percent_yields_no_alert()
    {
        var alice = await RegisterAndLoginAsync("budgetalertlow");
        var now = DateTime.UtcNow;

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Groceries", MonthlyLimit = 300m });

        var movedExpenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Groceries 1", 50m, "Groceries", now); // 50 = 16.7%
            var moved = SeedData.AddExpense(db, alice.Id, "Snack run", 20m, "Snacks", now); // -> 70 = 23.3% once moved
            await db.SaveChangesAsync();
            movedExpenseId = moved.Id;
        });

        var res = await alice.Client.PutAsJsonAsync($"/api/Expense/{movedExpenseId}",
            new UpdateExpenseDto(movedExpenseId.ToString(), "Snack run", "Snack run description", "Groceries"));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        (await GetBudgetAlertsAsync(alice.Client)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sequential_reassignments_cross_80_then_100_with_exactly_one_alert_each()
    {
        var alice = await RegisterAndLoginAsync("budgetalertseq");
        var now = DateTime.UtcNow;

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Groceries", MonthlyLimit = 300m });

        var firstMoveId = Guid.Empty;
        var secondMoveId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Groceries base", 210m, "Groceries", now); // 70%
            var first = SeedData.AddExpense(db, alice.Id, "Snack A", 30m, "Snacks", now);   // -> 240 = 80% once moved
            var second = SeedData.AddExpense(db, alice.Id, "Snack B", 65m, "Snacks", now);  // -> 305 = 101.67% once moved
            await db.SaveChangesAsync();
            firstMoveId = first.Id;
            secondMoveId = second.Id;
        });

        // 70% -> 80%: crosses the 80% threshold only.
        var firstRes = await alice.Client.PutAsJsonAsync($"/api/Expense/{firstMoveId}",
            new UpdateExpenseDto(firstMoveId.ToString(), "Snack A", "Snack A description", "Groceries"));
        firstRes.StatusCode.Should().Be(HttpStatusCode.OK, await firstRes.Content.ReadAsStringAsync());

        var alertsAfterFirst = await GetBudgetAlertsAsync(alice.Client);
        alertsAfterFirst.Should().ContainSingle();
        alertsAfterFirst[0].Message.Should().Be("You've used 80% of your $300 Groceries budget this month");

        // 80% -> ~101.67%: crosses the 100% threshold only, no repeat 80% alert.
        var secondRes = await alice.Client.PutAsJsonAsync($"/api/Expense/{secondMoveId}",
            new UpdateExpenseDto(secondMoveId.ToString(), "Snack B", "Snack B description", "Groceries"));
        secondRes.StatusCode.Should().Be(HttpStatusCode.OK, await secondRes.Content.ReadAsStringAsync());

        var alertsAfterSecond = await GetBudgetAlertsAsync(alice.Client);
        alertsAfterSecond.Should().HaveCount(2);
        alertsAfterSecond.Should().ContainSingle(a => a.Message == "You've used 80% of your $300 Groceries budget this month");
        alertsAfterSecond.Should().ContainSingle(a => a.Message == "You've used 102% of your $300 Groceries budget this month");
    }

    [Fact]
    public async Task Reassignment_within_80_to_99_percent_after_already_crossing_80_yields_no_new_alert()
    {
        var alice = await RegisterAndLoginAsync("budgetalertmid");
        var now = DateTime.UtcNow;

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Groceries", MonthlyLimit = 300m });

        var movedExpenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Groceries base", 250m, "Groceries", now); // already 83.3%
            var moved = SeedData.AddExpense(db, alice.Id, "Snack", 10m, "Snacks", now);  // -> 260 = 86.7% once moved, still <100%
            await db.SaveChangesAsync();
            movedExpenseId = moved.Id;
        });

        var res = await alice.Client.PutAsJsonAsync($"/api/Expense/{movedExpenseId}",
            new UpdateExpenseDto(movedExpenseId.ToString(), "Snack", "Snack description", "Groceries"));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        (await GetBudgetAlertsAsync(alice.Client)).Should().BeEmpty();
    }

    [Fact]
    public async Task Uncategorised_expenses_count_against_other_budget()
    {
        var alice = await RegisterAndLoginAsync("budgetalertother");
        var now = DateTime.UtcNow;

        await alice.Client.PutAsJsonAsync("/api/Category", new UpsertCategoryDto { Name = "Other", MonthlyLimit = 100m });

        var movedExpenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, alice.Id, "Misc 1", 60m, null, now); // uncategorised -> Other bucket, 60%
            var moved = SeedData.AddExpense(db, alice.Id, "Misc 2", 25m, "Temp", now); // -> 85% once moved to uncategorised
            await db.SaveChangesAsync();
            movedExpenseId = moved.Id;
        });

        var res = await alice.Client.PutAsJsonAsync($"/api/Expense/{movedExpenseId}",
            new UpdateExpenseDto(movedExpenseId.ToString(), "Misc 2", "Misc 2 description", null));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var alerts = await GetBudgetAlertsAsync(alice.Client);
        alerts.Should().ContainSingle();
        alerts[0].Message.Should().Be("You've used 85% of your $100 Other budget this month");
    }
}
