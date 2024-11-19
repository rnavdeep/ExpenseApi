using System;
using Expense.API.Models.DTO;

namespace Expense.API.Repositories.FriendRequest
{
	public interface IFriendRequestRepository
	{
        Task SendRequest(string id, string userName);
        Task AcceptRequest(string requestId);
        Task <List<FriendsListDto>> GetFriends();
        Task<List<UserDto>> GetDropdownUsers();

    }
}

