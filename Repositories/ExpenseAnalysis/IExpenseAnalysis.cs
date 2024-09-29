using Expense.API.Models.Domain;

namespace Expense.API.Repositories.ExpenseAnalysis
{
	public interface IExpenseAnalysis
	{
        public Task<List<ExpenseDocumentResult>> StartExpenseExtractAsync(Guid expenseId);
    }
}

