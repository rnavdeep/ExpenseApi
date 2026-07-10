using System;
namespace Expense.API.Models.Domain
{
	public class Budget
	{
		public Budget()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// Owner of the budget.
        /// </summary>
        public Guid UserId { get; set; }
        public User User { get; set; }

        public string Category { get; set; }

        public decimal MonthlyLimit { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
