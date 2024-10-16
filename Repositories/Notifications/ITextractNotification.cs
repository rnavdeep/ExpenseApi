using System;
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.Notifications
{
	public interface ITextractNotification
	{
		Task<Guid> CreateNotifcation(Guid userId,string message,string title);
		Task ReadAllNotifications();
        Task<List<Notification>> GetNotifications();
	}
}

