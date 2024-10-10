using Expense.API.Models.Domain;

namespace Expense.API.Repositories.ExpenseAnalysis
{
	public interface IExpenseAnalysis
	{
        public Task<List<ExpenseDocumentResult>> StartExpenseExtractAsync(Guid expenseId);
        public Task<DocumentResult> StartExpenseExtractByDocIdAsync(Guid expenseId, Guid docId);
        public Task<string> StartExpenseExtractByDocIdJobIdAsync(Guid expenseId, Guid docId);


    }
}

