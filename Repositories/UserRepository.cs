using System;
using Microsoft.EntityFrameworkCore;
using NSWalks.API.Data;
using NSWalks.API.Models.Domain;

namespace NSWalks.API.Repositories
{
	public class UserRepository:IUserRepository
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
		public UserRepository(UserDocumentsDbContext userDocumentsDbContext)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
		}

        public async Task<User> CreateAsync(User user)
        {
            //get difficulty object for id
            var userFound = await userDocumentsDbContext.Users.FirstOrDefaultAsync(a => a.Username.ToUpper().Equals(user.Username.ToUpper()));
            if (userFound != null)
            {
                throw new Exception("User already exists");
            }
            //use domain model to create Region in database
            await userDocumentsDbContext.Users.AddAsync(user);

            await userDocumentsDbContext.SaveChangesAsync();

            return user;
        }

        public async Task<User>? GetUserByEmail(string email)
        {
            var userFound = await userDocumentsDbContext.Users.FirstOrDefaultAsync(a => a.Email.ToUpper().Equals(email.ToUpper()));
            return userFound;
        }
    }
}

