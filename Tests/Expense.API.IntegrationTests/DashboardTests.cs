using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// Exercises the dashboard endpoints against a hand-built, deterministic scenario and asserts the
/// exact aggregated numbers. Mirrors the math in ExpenseRepository.GetDashboardSummaryAsync /
/// GetMonthlySpendingAsync / GetOutstandingBalancesAsync.
/// </summary>
public class DashboardTests : IntegrationTestBase
{
    public DashboardTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Summary_reports_totals_categories_and_owe_amounts()
    {
        var alice = await RegisterAndLoginAsync("alice");
        var bob = await CreateBusinessUserAsync("bob");
        var carol = await CreateBusinessUserAsync("carol");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            // Alice's own spending this month: 100 + 50 + 200 + 30 (uncategorised) = 380.
            var travel = SeedData.AddExpense(db, alice.Id, "Flight", 200m, "Travel", now);
            SeedData.AddExpense(db, alice.Id, "Groceries", 100m, "Groceries", now);
            SeedData.AddExpense(db, alice.Id, "Dinner", 50m, "Dining", now);
            SeedData.AddExpense(db, alice.Id, "Misc", 30m, null, now);

            // Two receipts uploaded by Alice this month.
            SeedData.AddReceipt(db, alice.Id, travel.Id, now);
            SeedData.AddReceipt(db, alice.Id, travel.Id, now);

            // Bob & Carol owe Alice on her expense: 25 + 15 = 40 (OwedToYou).
            SeedData.AddShare(db, travel.Id, bob.Id, 25);
            SeedData.AddShare(db, travel.Id, carol.Id, 15);

            // Alice owes Bob 40 on an expense Bob created (YouOwe).
            var bobExpense = SeedData.AddExpense(db, bob.Id, "Concert", 80m, "Entertainment", now);
            SeedData.AddShare(db, bobExpense.Id, alice.Id, 40);

            await db.SaveChangesAsync();
        });

        var summary = await alice.Client.GetFromJsonAsync<DashboardSummaryDto>("/api/Expense/summary?period=month");

        summary!.TotalSpent.Should().Be(380m);
        summary.ReceiptsScanned.Should().Be(2);
        summary.YouOwe.Should().Be(40.0);
        summary.OwedToYou.Should().Be(40.0);
        summary.TotalSpentDeltaPct.Should().Be(100.0); // no prior-period spend
        summary.Categories.Should().HaveCount(4);
        summary.Categories[0].Category.Should().Be("Travel"); // highest amount first
        summary.Categories[0].Amount.Should().Be(200m);
        summary.Categories.Should().Contain(c => c.Category == "Other" && c.Amount == 30m);
    }

    [Fact]
    public async Task Balances_group_amounts_by_counterparty_in_both_directions()
    {
        var alice = await RegisterAndLoginAsync("baluser");
        var bob = await CreateBusinessUserAsync("balbob");
        var carol = await CreateBusinessUserAsync("balcarol");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            var aliceExpense = SeedData.AddExpense(db, alice.Id, "Hotel", 300m, "Travel", now);
            SeedData.AddShare(db, aliceExpense.Id, bob.Id, 120);
            SeedData.AddShare(db, aliceExpense.Id, carol.Id, 80);

            var bobExpense = SeedData.AddExpense(db, bob.Id, "Rental", 90m, "Travel", now);
            SeedData.AddShare(db, bobExpense.Id, alice.Id, 45);

            await db.SaveChangesAsync();
        });

        var balances = await alice.Client.GetFromJsonAsync<OutstandingBalancesDto>("/api/Expense/balances");

        balances!.OwedToYou.Should().HaveCount(2);
        balances.OwedToYou[0].UserName.Should().Be(bob.Username); // 120 sorted before 80
        balances.OwedToYou[0].Amount.Should().Be(120.0);
        balances.OwedToYou.Should().Contain(b => b.UserName == carol.Username && b.Amount == 80.0);

        balances.YouOwe.Should().ContainSingle();
        balances.YouOwe[0].UserName.Should().Be(bob.Username);
        balances.YouOwe[0].Amount.Should().Be(45.0);
    }

    [Fact]
    public async Task Monthly_returns_zero_filled_series_with_correct_amounts()
    {
        var dave = await RegisterAndLoginAsync("dave");
        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        DateTime MonthAnchor(int monthsAgo) => firstOfMonth.AddMonths(-monthsAgo).AddDays(14);

        await WithDbAsync(async db =>
        {
            SeedData.AddExpense(db, dave.Id, "This month", 100m, "Groceries", MonthAnchor(0));
            SeedData.AddExpense(db, dave.Id, "Last month", 60m, "Dining", MonthAnchor(1));
            SeedData.AddExpense(db, dave.Id, "Three months ago", 40m, "Travel", MonthAnchor(3));
            await db.SaveChangesAsync();
        });

        var series = await dave.Client.GetFromJsonAsync<List<MonthlySpendingDto>>("/api/Expense/monthly?months=6");

        series!.Should().HaveCount(6); // oldest first, current month last
        series[5].Amount.Should().Be(100m);
        series[4].Amount.Should().Be(60m);
        series[2].Amount.Should().Be(40m);
        series[0].Amount.Should().Be(0m);
        series[5].Label.Should().Be(now.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Summary_with_no_data_returns_zeroes()
    {
        var lonely = await RegisterAndLoginAsync("lonely");

        var summary = await lonely.Client.GetFromJsonAsync<DashboardSummaryDto>("/api/Expense/summary?period=month");

        summary!.TotalSpent.Should().Be(0m);
        summary.ReceiptsScanned.Should().Be(0);
        summary.YouOwe.Should().Be(0.0);
        summary.OwedToYou.Should().Be(0.0);
        summary.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_requires_authentication()
    {
        var res = await Factory.CreateClient().GetAsync("/api/Expense/summary");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
