using System;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Repositories.ExpenseAnalysis;
using Expense.API.Repositories.Notifications;

namespace Expense.API.Repositories.Background
{
	public interface IBackgroundPollingRepository
	{
		Task PollTextractJob(string jobId, DocumentJobResult documentJobResult,
                                           UserDocumentsDbContext userDocumentsDbContext,
                                           IExpenseAnalysis expenseAnalysis,
                                           CancellationToken stoppingToken, ITextractNotification textractNotificationDb);
	}
}

