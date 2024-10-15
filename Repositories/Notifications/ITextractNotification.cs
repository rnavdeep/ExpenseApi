using System;
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.Notifications
{
	public interface ITextractNotification
	{
		Task<Guid> CreateNotifcation(Guid userId,string message);
		Task ReadAllNotifications();
        Task<List<Notification>> GetNotifications();
	}
}

