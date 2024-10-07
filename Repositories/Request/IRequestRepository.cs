using System;
namespace Expense.API.Repositories.Request
{
	public interface IRequestRepository
	{
        public string DecryptData(string data);

    }
}

