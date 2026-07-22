using System;
namespace Expense.API.Models.Domain
{
	public class Category
	{
		public Category()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// Owner of the category.
        /// </summary>
        public Guid UserId { get; set; }
        public User User { get; set; }

        public string Name { get; set; }

        public decimal MonthlyLimit { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
