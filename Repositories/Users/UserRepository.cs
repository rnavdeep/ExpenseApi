using System;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Microsoft.Extensions.DependencyInjection;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Expense.API.Repositories.Users
{
	public class UserRepository:IUserRepository
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHubContext<TextractNotificationHub> textractNotification;
        private IServiceProvider serviceProvider;

        public UserRepository(UserDocumentsDbContext userDocumentsDbContext, IServiceProvider serviceProvider,
            IHubContext<TextractNotificationHub> textractNotification)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.textractNotification = textractNotification;
            this.serviceProvider = serviceProvider;
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

        public async Task SendRequest(string id, string userName)
        {
            // Use the service provider to create a scope
            using (var scope = serviceProvider.CreateScope())
            {
                var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                await textractNotificationDb.CreateNotifcation(Guid.Parse(id), "Friend Request sent by user", "New Friend Request");
            }
            // Use the service provider to create a scope
            await textractNotification.Clients.User(userName.ToString()).SendAsync("TextractNotification", "Friend Request Received");
        }
    }
}

