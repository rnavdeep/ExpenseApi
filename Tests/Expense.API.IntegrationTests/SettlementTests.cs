using System.Net;
using System.Net.Http.Json;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Models.DTO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Expense.API.IntegrationTests;

public class SettlementTests : IntegrationTestBase
{
    public SettlementTests(CustomWebAppFactory factory) : base(factory) { }

    [Fact]
    public async Task Create_persists_settlement_and_returns_201()
    {
        var alice = await RegisterAndLoginAsync("setalice");
        var bob = await CreateBusinessUserAsync("setbob");

        var res = await alice.Client.PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = bob.Id,
            Amount = 42.50m,
            Note = "lunch"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<SettlementDto>();
        dto!.PayerUserId.Should().Be(alice.Id);
        dto.PayerUserName.Should().Be(alice.UserName);
        dto.PayeeUserId.Should().Be(bob.Id);
        dto.PayeeUserName.Should().Be(bob.Username);
        dto.Amount.Should().Be(42.50m);
        dto.Note.Should().Be("lunch");

        await WithDbAsync(async db =>
        {
            var row = await db.Settlements.FirstOrDefaultAsync(s => s.Id == dto.Id);
            row.Should().NotBeNull();
            row!.PayerId.Should().Be(alice.Id);
            row.PayeeId.Should().Be(bob.Id);
            row.Amount.Should().Be(42.50m);
        });
    }

    [Fact]
    public async Task Create_rejects_non_positive_amount()
    {
        var alice = await RegisterAndLoginAsync("setzero");
        var bob = await CreateBusinessUserAsync("setzerobob");

        var res = await alice.Client.PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = bob.Id,
            Amount = 0m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_unknown_payee()
    {
        var alice = await RegisterAndLoginAsync("setghost");

        var res = await alice.Client.PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = Guid.NewGuid(),
            Amount = 10m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_settling_with_self()
    {
        var alice = await RegisterAndLoginAsync("setself");

        var res = await alice.Client.PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = alice.Id,
            Amount = 10m
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_lists_settlements_for_both_payer_and_payee_newest_first()
    {
        var alice = await RegisterAndLoginAsync("setlistalice");
        var bob = await CreateBusinessUserAsync("setlistbob");
        var carol = await CreateBusinessUserAsync("setlistcarol");

        // Alice pays Bob, then Carol pays Alice - both should show up for Alice, newest first.
        var first = await alice.Client.PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = bob.Id,
            Amount = 20m
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        await WithDbAsync(async db =>
        {
            var settlement = new Expense.API.Models.Domain.Settlement
            {
                Id = Guid.NewGuid(),
                PayerId = carol.Id,
                PayeeId = alice.Id,
                Amount = 15m,
                CreatedAt = DateTime.UtcNow.AddMinutes(1)
            };
            db.Settlements.Add(settlement);
            await db.SaveChangesAsync();
        });

        var res = await alice.Client.GetAsync("/api/Settlement?pageNumber=1&pageSize=10");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<SettlementDto>>();

        list.Should().HaveCount(2);
        list![0].PayerUserId.Should().Be(carol.Id); // newest first
        list[1].PayeeUserId.Should().Be(bob.Id);
    }

    [Fact]
    public async Task Get_returns_not_found_when_caller_has_no_settlements()
    {
        var lonely = await RegisterAndLoginAsync("setlonely");

        var res = await lonely.Client.GetAsync("/api/Settlement");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var getRes = await Factory.CreateClient().GetAsync("/api/Settlement");
        getRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var postRes = await Factory.CreateClient().PostAsJsonAsync("/api/Settlement", new CreateSettlementDto
        {
            PayeeUserId = Guid.NewGuid(),
            Amount = 5m
        });
        postRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
