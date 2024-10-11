using System;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Repositories.ExpenseAnalysis;

namespace Expense.API.Repositories.Background
{
	public interface IBackgroundPollingRepository
	{
		Task PollTextractJob(string jobId, DocumentJobResult documentJobResult,
                                           UserDocumentsDbContext userDocumentsDbContext,
                                           IExpenseAnalysis expenseAnalysis,
                                           CancellationToken stoppingToken);
	}
}

