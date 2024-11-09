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

        public async Task SendRequest(string id, string userName)
        {
            var userRequestFrom = httpContextAccessor.HttpContext?.User?.Claims
                          .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not found.");
            }

            // Check if the user exists in the database
            var userRequestFromId = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == userRequestFrom.ToLower());
            var userRequestToId = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Id.Equals(Guid.Parse(id)));

            if(userRequestFromId != null && userRequestToId != null)
            {
                // Use the service provider to create a scope
                using (var scope = serviceProvider.CreateScope())
                {
                    var isRequestAlreadySent = await userDocumentsDbContext.FriendRequests.FirstOrDefaultAsync(
                        fr => fr.SentByUserId.Equals(userRequestFromId.Id) && fr.SentToUserId.Equals(userRequestToId.Id));

                    if (isRequestAlreadySent != null)
                    {
                        throw new Exception("Request already sent");
                    }
                    var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                    var notificationId = await textractNotificationDb.CreateNotifcation(userRequestToId.Id,
                        $"Friend Request sent by user {userRequestFromId.Username}", "New Friend Request", 1);

                    var friendRequest = new FriendRequest();
                    friendRequest.SentByUserId = userRequestFromId.Id;
                    friendRequest.SentToUserId = userRequestToId.Id;
                    friendRequest.NotificationId = notificationId;
                    friendRequest.IsAccepted = 0;
                    friendRequest.CreatedAt = DateTime.UtcNow;
                    await userDocumentsDbContext.FriendRequests.AddAsync(friendRequest);
                    await userDocumentsDbContext.SaveChangesAsync();
                }
                // Use the service provider to create a scope
                await textractNotification.Clients.User(userName.ToString()).SendAsync("TextractNotification", "Friend Request Received");
            }
            throw new Exception("Users not found");
        }
    }
}

