using System;
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.Users
{
	public interface IUserRepository
	{
        Task<User> CreateAsync(User user);
        Task<User>? GetUserByEmail(string email);
    }
}

