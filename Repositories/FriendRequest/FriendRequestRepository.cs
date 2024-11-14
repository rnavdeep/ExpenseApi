using Expense.API.Data;
using Expense.API.Repositories.Notifications;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FriendRequestModel = Expense.API.Models.Domain.FriendRequest;
using Expense.API.Models.DTO;

namespace Expense.API.Repositories.FriendRequest
{
    public class FriendRequestRespository : IFriendRequestRepository
    {
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHubContext<TextractNotificationHub> textractNotification;
        private IServiceProvider serviceProvider;
        private readonly IHttpContextAccessor httpContextAccessor;

        public FriendRequestRespository(UserDocumentsDbContext userDocumentsDbContext, IServiceProvider serviceProvider,
            IHubContext<TextractNotificationHub> textractNotification, IHttpContextAccessor httpContextAccessor)
        {
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.textractNotification = textractNotification;
            this.serviceProvider = serviceProvider;
            this.httpContextAccessor = httpContextAccessor;
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

            if (userRequestFromId != null && userRequestToId != null)
            {
                // Use the service provider to create a scope
                using (var scope = serviceProvider.CreateScope())
                {
                    var isRequestAlreadySent = await userDocumentsDbContext.FriendRequests.FirstOrDefaultAsync(
                        fr => fr.SentByUserId.Equals(userRequestFromId.Id) && fr.SentToUserId.Equals(userRequestToId.Id));
                    var isRequestAlreadyReceived = await userDocumentsDbContext.FriendRequests.FirstOrDefaultAsync(
                        fr => fr.SentByUserId.Equals(userRequestToId.Id) && fr.SentToUserId.Equals(userRequestFromId.Id));

                    if (isRequestAlreadySent != null || isRequestAlreadyReceived != null)
                    {
                        throw new Exception("Request already sent");
                    }
                    var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                    var notificationId = await textractNotificationDb.CreateNotifcation(userRequestToId.Id,
                        $"Friend Request sent by user {userRequestFromId.Username}", "New Friend Request", 1);

                    var friendRequest = new FriendRequestModel();
                    friendRequest.SentByUserId = userRequestFromId.Id;
                    friendRequest.SentToUserId = userRequestToId.Id;
                    friendRequest.NotificationId = notificationId;
                    friendRequest.IsAccepted = 0;
                    friendRequest.CreatedAt = DateTime.UtcNow;
                    friendRequest.AcceptedAt = null;
                    await userDocumentsDbContext.FriendRequests.AddAsync(friendRequest);
                    await userDocumentsDbContext.SaveChangesAsync();
                }
                // Use the service provider to create a scope
                await textractNotification.Clients.User(userName.ToString()).SendAsync("TextractNotification", "Friend Request Received");
                return;
            }
            throw new Exception("Users not found");
        }

        public async Task AcceptRequest(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                throw new Exception("Request can not be null");
            }

            var notificationId = Guid.Parse(requestId);

            var friendRequest = await userDocumentsDbContext.FriendRequests
                .Where(fr => fr.NotificationId == notificationId)
                .Join(userDocumentsDbContext.Notification,
                      fr => fr.NotificationId,
                      n => n.Id,
                      (fr, n) => fr)
                .FirstOrDefaultAsync();

            if (friendRequest != null)
            {
                // Accept the request
                friendRequest.IsAccepted = 1;
                friendRequest.AcceptedAt = DateTime.UtcNow;

                // Use the service provider to create a scope to send accepted notification
                using (var scope = serviceProvider.CreateScope())
                {
                    var userAcceptedNotification = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                        u => u.Id.Equals(friendRequest.SentByUserId));
                    var userReceivedNotification = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                        u => u.Id.Equals(friendRequest.SentToUserId));
                    var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                    if (userAcceptedNotification != null && userReceivedNotification != null)
                    {
                        await textractNotificationDb.CreateNotifcation(userAcceptedNotification.Id,
                                $"Friend Request accepted by user {userReceivedNotification.Username}", "Friend Request Accepted", 0);

                        await userDocumentsDbContext.SaveChangesAsync();
                        await textractNotification.Clients.User(userAcceptedNotification.Username.ToString()).SendAsync("TextractNotification", "Friend Request Received");
                    }

                }
                return;
            }

            throw new Exception("Error performing action");
        }

        public async Task<List<FriendsListDto>> GetFriends()
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
              .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not found.");
            }

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == userName.ToLower());

            if (user != null)
            {
                var friends = await userDocumentsDbContext.FriendRequests
                    .Where(friend =>
                        (friend.SentByUserId == user.Id || friend.SentToUserId == user.Id) &&
                        friend.IsAccepted == 1)
                    .Select(friend =>
                        new FriendsListDto
                        {
                            Username = friend.SentByUserId == user.Id ? friend.SentToUser.Username : friend.SentByUser.Username,
                            AcceptedAt = (DateTime)friend.AcceptedAt,
                            SharedExpenses = new List<ExpenseDto>()
                        })
                    .ToListAsync();
                return friends;
            }
            else
            {
                throw new Exception("User not found.");

            }
            throw new NotImplementedException();
        }
    }
}

