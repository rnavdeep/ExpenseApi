using System;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Microsoft.Extensions.DependencyInjection;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Expense.API.Repositories.Users
{
	public class UserRepository:IUserRepository
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHubContext<TextractNotificationHub> textractNotification;
        private IServiceProvider serviceProvider;
        private readonly IHttpContextAccessor httpContextAccessor;

        public UserRepository(UserDocumentsDbContext userDocumentsDbContext, IServiceProvider serviceProvider,
            IHubContext<TextractNotificationHub> textractNotification, IHttpContextAccessor httpContextAccessor)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.textractNotification = textractNotification;
            this.serviceProvider = serviceProvider;
            this.httpContextAccessor = httpContextAccessor;
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

        public async Task<User?> GetUserByEmail(string email)
        {
            var userFound = await userDocumentsDbContext.Users.FirstOrDefaultAsync(a => a.Email.ToUpper().Equals(email.ToUpper()));
            return userFound;
        }

        public async Task<User?> GetUserByUserName(string userName)
        {
            var userFound = await userDocumentsDbContext.Users.FirstOrDefaultAsync(a => a.Username.ToUpper().Equals(userName.ToUpper()));
            return userFound;
        }

    }
}

