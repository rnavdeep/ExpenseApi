using System;
namespace Expense.API.Repositories.Redis
{
	public interface IRedisRepository
	{
        Task StoreTokenAsync(string userName, string token);
        Task<string> GetTokenAsync(string userName);
        Task DeleteTokenAsync(string userName);
    }
}

