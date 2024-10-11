using Amazon.Textract.Model;
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.ExpenseAnalysis
{
	public interface IExpenseAnalysis
	{
        public Task<List<ExpenseDocumentResult>> StartExpenseExtractAsync(Guid expenseId);
        //public Task<DocumentJobResult> StartExpenseExtractByDocIdAsync(Guid expenseId, Guid docId);
        public Task<string> StartExpenseExtractByDocIdJobIdAsync(Guid expenseId, Guid docId);
        Task StoreResults(GetExpenseAnalysisResponse getExpenseAnalysisResponse, DocumentJobResult documentJobResult, byte status);

    }
}

