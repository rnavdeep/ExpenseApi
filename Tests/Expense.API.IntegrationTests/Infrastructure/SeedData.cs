using Expense.API.Data;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.IntegrationTests.Infrastructure;

/// <summary>
/// Small builders for inserting realistic business data (expenses, receipts, splits) that light up
/// the dashboard aggregations in ExpenseRepository. Operate on a tracked DbContext; the caller saves.
/// </summary>
public static class SeedData
{
    public static ExpenseModel AddExpense(UserDocumentsDbContext db, Guid createdById, string title,
        decimal amount, string? category, DateTime createdAt)
    {
        var expense = new ExpenseModel
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"{title} description",
            Amount = amount,
            Category = category,
            CreatedById = createdById,
            CreatedAt = createdAt
        };
        db.Expenses.Add(expense);
        return expense;
    }

    public static Document AddReceipt(UserDocumentsDbContext db, Guid userId, Guid expenseId, DateTime uploadedAt)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            FileName = $"receipt-{Guid.NewGuid():N}.pdf",
            FileExtension = ".pdf",
            S3Url = "https://example.com/receipt",
            ETag = "etag",
            VersionId = "v1",
            Size = 1024,
            UploadedAt = uploadedAt,
            UserId = userId,
            ExpenseId = expenseId
        };
        db.Documents.Add(doc);
        return doc;
    }

    /// <summary>A user's split on an expense: UserAmount is what dashboard owe/balance sums use.</summary>
    public static ExpenseUser AddShare(UserDocumentsDbContext db, Guid expenseId, Guid userId,
        double userAmount, double userShare = 0.5)
    {
        var share = new ExpenseUser(expenseId, userId, userAmount) { UserShare = userShare };
        db.ExpenseUsers.Add(share);
        return share;
    }

    /// <summary>A scanned-receipt result for a document: Total feeds the ScannedReceiptsTotal floor.</summary>
    public static DocumentJobResult AddDocumentJobResult(UserDocumentsDbContext db, Guid createdById,
        Guid expenseId, Guid documentId, decimal total, byte status = 1)
    {
        var jobResult = new DocumentJobResult
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid().ToString(),
            Status = status,
            CreatedById = createdById,
            ExpenseId = expenseId,
            DocumentId = documentId,
            Total = total
        };
        db.DocumentJobResults.Add(jobResult);
        return jobResult;
    }
}
