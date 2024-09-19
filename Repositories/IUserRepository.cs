using System;
using NSWalks.API.Models.Domain;

namespace NSWalks.API.Repositories
{
	public interface IUserRepository
	{
        Task<User> CreateAsync(User user);
        Task<User>? GetUserByEmail(string email);
    }
}

