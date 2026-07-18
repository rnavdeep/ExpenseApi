using System.Linq;
using Amazon.Textract;
using Amazon.Textract.Model;
using Expense.API.Data;
using Expense.API.IntegrationTests.Infrastructure;
using Expense.API.Repositories.ExpenseAnalysis;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Expense.API.IntegrationTests;

/// <summary>
/// Exercises the line-item assignment business rules directly against IExpenseRepository (there is no
/// HTTP surface yet - that's phase B10): friendship-gated assign/remove, additive bulk-assign, and the
/// ExpenseUser recompute that derives per-person totals from assignments. Also exercises
/// ExpenseAnalysis.StoreResults' first-scan carry-forward of existing ExpenseUser rows onto new
/// LineItems.
/// </summary>
public class LineItemAssignmentTests : IntegrationTestBase
{
    public LineItemAssignmentTests(CustomWebAppFactory factory) : base(factory) { }

    private static LineItemFields BuildLineItemFields(string item, string price)
    {
        return new LineItemFields
        {
            LineItemExpenseFields = new List<ExpenseField>
            {
                new ExpenseField { Type = new ExpenseType { Text = "ITEM" }, ValueDetection = new ExpenseDetection { Text = item } },
                new ExpenseField { Type = new ExpenseType { Text = "PRICE" }, ValueDetection = new ExpenseDetection { Text = price } }
            }
        };
    }

    [Fact]
    public async Task AssignUserToLineItem_with_a_non_friend_throws_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("liaowner");
        var stranger = await RegisterAndLoginAsync("liastranger");

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

        Func<Task> act = () => WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToLineItemAsync(itemId, stranger.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Users must be friends before sharing an expense.");
    }

    [Fact]
    public async Task AssignUserToLineItem_with_an_accepted_friend_recomputes_an_even_split_across_line_items()
    {
        var owner = await RegisterAndLoginAsync("liaowner");
        var friend = await RegisterAndLoginAsync("liafriend");
        await BefriendAsync(owner, friend);

        Guid expenseId = Guid.Empty, item1Id = Guid.Empty, item2Id = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Grocery run", 60m, "Groceries", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 60m);
            var item1 = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Milk", 30m, owner.Id);
            var item2 = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Bread", 30m, owner.Id);
            SeedData.AddShare(db, expense.Id, owner.Id, 60, 1.0); // creator's own row, as CreateExpenseAsync would seed it
            await db.SaveChangesAsync();
            expenseId = expense.Id;
            item1Id = item1.Id;
            item2Id = item2.Id;
        });

        await WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToLineItemAsync(item1Id, friend.Id));
        var updatedItem2 = await WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToLineItemAsync(item2Id, friend.Id));

        updatedItem2.Assignments.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id, friend.Id });

        await WithDbAsync(async db =>
        {
            var expenseUsers = await db.ExpenseUsers.Where(eu => eu.ExpenseId == expenseId).ToListAsync();
            expenseUsers.Should().HaveCount(2);
            expenseUsers.First(eu => eu.UserId == owner.Id).UserAmount.Should().Be(30.0);
            expenseUsers.First(eu => eu.UserId == friend.Id).UserAmount.Should().Be(30.0);
        });
    }

    [Fact]
    public async Task AssignUserToLineItem_that_is_already_assigned_is_a_no_op()
    {
        var owner = await RegisterAndLoginAsync("liaowner");

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

        var result = await WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToLineItemAsync(itemId, owner.Id));

        result.Assignments.Should().ContainSingle(a => a.UserId == owner.Id);
    }

    [Fact]
    public async Task RemoveUserFromLineItem_last_remaining_assignee_throws_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("liaowner");

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

        Func<Task> act = () => WithExpenseRepositoryAsync(owner, repo => repo.RemoveUserFromLineItemAsync(itemId, owner.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Cannot remove the last remaining assignee from a line item.");
    }

    [Fact]
    public async Task RemoveUserFromLineItem_with_more_than_one_assignee_recomputes_shares()
    {
        var owner = await RegisterAndLoginAsync("liaowner");
        var friend = await RegisterAndLoginAsync("liafriend");
        await BefriendAsync(owner, friend);

        Guid expenseId = Guid.Empty, itemId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Lunch", 20m, "Dining", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 20m);
            var item = SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Sandwich", 20m, owner.Id, friend.Id);
            SeedData.AddShare(db, expense.Id, owner.Id, 10, 0.5);
            SeedData.AddShare(db, expense.Id, friend.Id, 10, 0.5);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
            itemId = item.Id;
        });

        var updated = await WithExpenseRepositoryAsync(owner, repo => repo.RemoveUserFromLineItemAsync(itemId, friend.Id));

        updated.Assignments.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id });

        await WithDbAsync(async db =>
        {
            var expenseUsers = await db.ExpenseUsers.Where(eu => eu.ExpenseId == expenseId).ToListAsync();
            expenseUsers.Should().ContainSingle();
            expenseUsers[0].UserId.Should().Be(owner.Id);
            expenseUsers[0].UserAmount.Should().Be(20.0);
        });
    }

    [Fact]
    public async Task AssignUserToAllLineItems_is_additive_and_never_removes_other_assignees()
    {
        var owner = await RegisterAndLoginAsync("liaowner");
        var friendA = await RegisterAndLoginAsync("liafrienda");
        var friendB = await RegisterAndLoginAsync("liafriendb");
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

        var updatedItems = await WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToAllLineItemsAsync(expenseId, friendB.Id));

        var item1Updated = updatedItems.Single(li => li.Id == item1Id);
        var item2Updated = updatedItems.Single(li => li.Id == item2Id);

        // Additive: friendA's pre-existing assignment on item1 is untouched, friendB is added to both.
        item1Updated.Assignments.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id, friendA.Id, friendB.Id });
        item2Updated.Assignments.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id, friendB.Id });
    }

    [Fact]
    public async Task AssignUserToAllLineItems_with_a_non_friend_throws_the_expected_message()
    {
        var owner = await RegisterAndLoginAsync("liaowner");
        var stranger = await RegisterAndLoginAsync("liastranger");

        Guid expenseId = Guid.Empty;
        await WithDbAsync(async db =>
        {
            var expense = SeedData.AddExpense(db, owner.Id, "Groceries", 20m, "Groceries", DateTime.UtcNow);
            var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
            var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 20m);
            SeedData.AddLineItem(db, expense.Id, jobResult.Id, "Milk", 20m, owner.Id);
            await db.SaveChangesAsync();
            expenseId = expense.Id;
        });

        Func<Task> act = () => WithExpenseRepositoryAsync(owner, repo => repo.AssignUserToAllLineItemsAsync(expenseId, stranger.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Users must be friends before sharing an expense.");
    }

    [Fact]
    public async Task StoreResults_first_scan_carries_forward_existing_expense_users_as_line_item_assignees()
    {
        var owner = await RegisterAndLoginAsync("liascanowner");
        var friend = await RegisterAndLoginAsync("liascanfriend");
        await BefriendAsync(owner, friend);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDocumentsDbContext>();
        var expenseAnalysis = scope.ServiceProvider.GetRequiredService<IExpenseAnalysis>();

        var expense = SeedData.AddExpense(db, owner.Id, "Dinner", 0m, "Dining", DateTime.UtcNow);
        var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
        SeedData.AddShare(db, expense.Id, owner.Id, 0, 0.5);
        SeedData.AddShare(db, expense.Id, friend.Id, 0, 0.5);
        var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 0m, status: 0);
        await db.SaveChangesAsync();

        var response = new GetExpenseAnalysisResponse
        {
            JobStatus = JobStatus.SUCCEEDED,
            ExpenseDocuments = new List<ExpenseDocument>
            {
                new ExpenseDocument
                {
                    SummaryFields = new List<ExpenseField>
                    {
                        new ExpenseField
                        {
                            Type = new ExpenseType { Text = "TOTAL" },
                            ValueDetection = new ExpenseDetection { Text = "20.00" }
                        }
                    },
                    LineItemGroups = new List<LineItemGroup>
                    {
                        new LineItemGroup
                        {
                            LineItems = new List<LineItemFields>
                            {
                                BuildLineItemFields("Coffee", "10.00"),
                                BuildLineItemFields("Bagel", "10.00")
                            }
                        }
                    }
                }
            }
        };

        await expenseAnalysis.StoreResults(response, jobResult, 1);

        var persistedItems = await db.LineItems
            .Where(li => li.ExpenseId == expense.Id)
            .Include(li => li.Assignments)
            .ToListAsync();

        persistedItems.Should().HaveCount(2);
        persistedItems.Should().OnlyContain(li =>
            li.Assignments.Select(a => a.UserId).OrderBy(id => id)
                .SequenceEqual(new[] { owner.Id, friend.Id }.OrderBy(id => id)));

        var expenseUsers = await db.ExpenseUsers.Where(eu => eu.ExpenseId == expense.Id).ToListAsync();
        expenseUsers.Should().HaveCount(2);
        expenseUsers.First(eu => eu.UserId == owner.Id).UserAmount.Should().Be(10.0);
        expenseUsers.First(eu => eu.UserId == friend.Id).UserAmount.Should().Be(10.0);
    }

    [Fact]
    public async Task StoreResults_with_no_pre_existing_expense_users_defaults_new_items_to_the_creator_only()
    {
        var owner = await RegisterAndLoginAsync("liascanowner2");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDocumentsDbContext>();
        var expenseAnalysis = scope.ServiceProvider.GetRequiredService<IExpenseAnalysis>();

        var expense = SeedData.AddExpense(db, owner.Id, "Coffee run", 0m, "Dining", DateTime.UtcNow);
        var receipt = SeedData.AddReceipt(db, owner.Id, expense.Id, DateTime.UtcNow);
        var jobResult = SeedData.AddDocumentJobResult(db, owner.Id, expense.Id, receipt.Id, 0m, status: 0);
        await db.SaveChangesAsync();

        var response = new GetExpenseAnalysisResponse
        {
            JobStatus = JobStatus.SUCCEEDED,
            ExpenseDocuments = new List<ExpenseDocument>
            {
                new ExpenseDocument
                {
                    SummaryFields = new List<ExpenseField>
                    {
                        new ExpenseField
                        {
                            Type = new ExpenseType { Text = "TOTAL" },
                            ValueDetection = new ExpenseDetection { Text = "5.00" }
                        }
                    },
                    LineItemGroups = new List<LineItemGroup>
                    {
                        new LineItemGroup
                        {
                            LineItems = new List<LineItemFields> { BuildLineItemFields("Latte", "5.00") }
                        }
                    }
                }
            }
        };

        await expenseAnalysis.StoreResults(response, jobResult, 1);

        var persistedItem = await db.LineItems
            .Include(li => li.Assignments)
            .SingleAsync(li => li.ExpenseId == expense.Id);
        persistedItem.Assignments.Select(a => a.UserId).Should().BeEquivalentTo(new[] { owner.Id });
    }
}
