using System;
namespace Expense.API.Models.Domain
{
	public class Settlement
	{
		public Settlement()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// User who paid.
        /// </summary>
        public Guid PayerId { get; set; }
        public User Payer { get; set; }

        /// <summary>
        /// User who was paid.
        /// </summary>
        public Guid PayeeId { get; set; }
        public User Payee { get; set; }

        public decimal Amount { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
