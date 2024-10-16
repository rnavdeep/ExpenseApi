using System;
namespace Expense.API.Models.Domain
{
	public class Notification
	{
		public Notification()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// Notification Created at time
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated when user opens the notification
        /// </summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// 0 - Unread
        /// 1 - Read
        /// </summary>
        public byte IsRead { get; set; } = 0;

         /// <summary>
         /// User notification created for
         /// </summary>
        public Guid UserId { get; set; }
        public User User { get; set; } // Navigation Property

        /// <summary>
        /// Message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Title
        /// </summary>
        public string? Title { get; set; }

    }
}

