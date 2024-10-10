using System;
namespace Expense.API.Repositories.Background
{
	public interface IBackgroundPollingRepository
	{
		Task<int> PollTextractJob(string jobId);
	}
}

