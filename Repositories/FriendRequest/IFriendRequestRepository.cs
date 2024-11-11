using System;
namespace Expense.API.Repositories.FriendRequest
{
	public interface IFriendRequestRepository
	{
        Task SendRequest(string id, string userName);
        Task AcceptRequest(string requestId);
    }
}

