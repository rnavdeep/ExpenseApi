using System;
namespace Expense.API.Models.Domain
{
	public class FriendRequest
	{
		public FriendRequest()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// Sent By User
        /// </summary>
        public Guid SentByUserId { get; set; }
        public User SentByUser { get; set; }

        /// <summary>
        /// Sent to user
        /// </summary>
        public Guid SentToUserId { get; set; }
        public User SentToUser { get; set; }

        /// <summary>
        /// Notification Associated with friend request
        /// </summary>
        public Guid NotificationId { get; set; }
        public Notification Notification { get; set; }

        /// <summary>
        /// Request Created At
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 0: Not Accepted, 1: Accepted
        /// </summary>
        public byte IsAccepted { get; set; } = 0;


        /// <summary>
        /// Request Accepted At
        /// </summary>
        public DateTime? AcceptedAt { get; set; } = DateTime.UtcNow;
    }
}

