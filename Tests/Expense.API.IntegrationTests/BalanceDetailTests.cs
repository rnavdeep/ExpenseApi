using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// Exercises GET /api/Expense/balances/{userId} against a hand-built ledger and asserts the
/// chronological entry list, directions, and the net summary. Mirrors the math in
/// ExpenseRepository.GetBalanceDetailAsync / GetNetBalancesByCounterpartyAsync.
/// </summary>
public class BalanceDetailTests : IntegrationTestBase
{
    public BalanceDetailTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Detail_returns_chronological_entries_with_directions_and_net()
    {
        var alice = await RegisterAndLoginAsync("baldetailalice");
        var bob = await CreateBusinessUserAsync("baldetailbob");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            var aliceExpense = SeedData.AddExpense(db, alice.Id, "Dinner", 100m, "Dining", now.AddMinutes(-30));
            SeedData.AddShare(db, aliceExpense.Id, bob.Id, 40); // bob owes alice 40

            var bobExpense = SeedData.AddExpense(db, bob.Id, "Cab", 60m, "Travel", now.AddMinutes(-20));
            SeedData.AddShare(db, bobExpense.Id, alice.Id, 25); // alice owes bob 25

            db.Settlements.Add(new Expense.API.Models.Domain.Settlement
            {
                Id = Guid.NewGuid(),
                PayerId = alice.Id,
                PayeeId = bob.Id,
                Amount = 25m,
                Note = "Cab payback",
                CreatedAt = now.AddMinutes(-10)
            });

            await db.SaveChangesAsync();
        });

        var detail = await alice.Client.GetFromJsonAsync<BalanceDetailDto>($"/api/Expense/balances/{bob.Id}");

        detail!.UserId.Should().Be(bob.Id);
        detail.UserName.Should().Be(bob.Username);
        // bob owes alice 40 from the expense; the 25 alice owed bob was settled by her payment.
        detail.NetAmount.Should().Be(40.0);
        detail.Direction.Should().Be("owedToYou");

        detail.Entries.Should().HaveCount(3);

        detail.Entries[0].Type.Should().Be("expense");
        detail.Entries[0].Description.Should().Be("Dinner");
        detail.Entries[0].Direction.Should().Be("owedToYou");
        detail.Entries[0].Amount.Should().Be(40.0);

        detail.Entries[1].Type.Should().Be("expense");
        detail.Entries[1].Description.Should().Be("Cab");
        detail.Entries[1].Direction.Should().Be("youOwe");
        detail.Entries[1].Amount.Should().Be(25.0);

        detail.Entries[2].Type.Should().Be("settlement");
        detail.Entries[2].Description.Should().Be("Cab payback");
        detail.Entries[2].Direction.Should().Be("youOwe");
        detail.Entries[2].Amount.Should().Be(25.0);
    }

    [Fact]
    public async Task Detail_reports_settled_when_net_is_zero()
    {
        var alice = await RegisterAndLoginAsync("baldetailzero");
        var bob = await CreateBusinessUserAsync("baldetailzerobob");
        var now = DateTime.UtcNow;

        await WithDbAsync(async db =>
        {
            var aliceExpense = SeedData.AddExpense(db, alice.Id, "Groceries", 60m, "Groceries", now);
            SeedData.AddShare(db, aliceExpense.Id, bob.Id, 30); // bob owes alice 30

            db.Settlements.Add(new Expense.API.Models.Domain.Settlement
            {
                Id = Guid.NewGuid(),
                PayerId = bob.Id,
                PayeeId = alice.Id,
                Amount = 30m,
                CreatedAt = now.AddMinutes(1)
            });

            await db.SaveChangesAsync();
        });

        var detail = await alice.Client.GetFromJsonAsync<BalanceDetailDto>($"/api/Expense/balances/{bob.Id}");

        detail!.NetAmount.Should().Be(0.0);
        detail.Direction.Should().Be("settled");
        detail.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Detail_returns_not_found_for_unknown_user()
    {
        var alice = await RegisterAndLoginAsync("baldetailghost");

        var res = await alice.Client.GetAsync($"/api/Expense/balances/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_requires_authentication()
    {
        var res = await Factory.CreateClient().GetAsync($"/api/Expense/balances/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
