using System;
using Expense.API.Models.Domain;

namespace Expense.API.Models.DTO
{
	public class NotificationDto
	{
		public NotificationDto()
		{
		}
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public byte IsRead { get; set; }
        public Guid UserId { get; set; }
        public string Message { get; set; }
        public string? Title { get; set; }
        public byte? IsFriendRequest { get; set; }

    }
}

