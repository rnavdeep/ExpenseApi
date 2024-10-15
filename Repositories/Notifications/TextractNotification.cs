using System;
using System.Security.Claims;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expense.API.Repositories.Notifications
{
	public class TextractNotification:ITextractNotification
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        public TextractNotification(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.httpContextAccessor = httpContextAccessor;
		}

        public async Task<Guid> CreateNotifcation(Guid userId, string message)
        {
            try
            {
                // Create notification
                var notification = new Notification();
                notification.CreatedAt = DateTime.UtcNow;
                notification.IsRead = 0;
                notification.Message = message;
                notification.UserId = userId;

                // Add to Db
                await userDocumentsDbContext.Notification.AddAsync(notification);
                await userDocumentsDbContext.SaveChangesAsync();

                // Return Created notification ID
                return notification.Id;
            }
            catch(Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<List<Notification>> GetNotifications()
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                         .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new Exception("User not found.");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == userName.ToLower());

            if (userFound == null)
            {
                throw new Exception("User does not exist.");
            }
            try
            {
                //fetch and return list of all notifications which belong to logged in user
                var c = await userDocumentsDbContext.Notification.Where(notification => notification.UserId.Equals(userFound.Id)).OrderByDescending(notification=>notification.CreatedAt).ToListAsync();
                return c;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }


        }

        public async Task ReadAllNotifications()
        {
            // Get the username from the HTTP context
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                         .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userName))
            {
                throw new UnauthorizedAccessException("User not found in claims.");
            }

            // Check if the user exists in the database
            var userFound = await userDocumentsDbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == userName.ToLower());

            if (userFound == null)
            {
                throw new Exception("User does not exist.");
            }

            try
            {
                // Fetch unread notifications for the user
                var notifications = await userDocumentsDbContext.Notification
                    .Where(n => n.UserId == userFound.Id && n.IsRead == 0)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                if (notifications.Any())
                {
                    // Mark all fetched notifications as read
                    foreach (var notification in notifications)
                    {
                        notification.IsRead = 1;
                        notification.ReadAt = DateTime.UtcNow;
                    }

                    // Save all changes
                    await userDocumentsDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the exception and rethrow or handle it
                throw new Exception("An error occurred while updating notifications.", ex);
            }
        }

    }
}

