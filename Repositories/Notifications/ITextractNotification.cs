using System;
namespace Expense.API.Repositories.Notifications
{
	public interface ITextractNotification
	{
		Task SendTextractNotification(string userId, string message);
	}
}

