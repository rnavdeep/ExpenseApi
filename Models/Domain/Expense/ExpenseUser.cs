using System;
namespace NSWalks.API.Models.Domain
{
	public class ExpenseUser
	{
		public ExpenseUser(Guid expenseId, Guid userId)
		{
            this.ExpenseId = expenseId;
            this.UserId = userId;
		}
        /// <summary>
        /// Foreign key for the associated Expense.
        /// </summary>
        public Guid ExpenseId { get; set; }

        /// <summary>
        /// Navigation property to the associated Expense.
        /// </summary>
        public Expense Expense { get; set; }

        /// <summary>
        /// Foreign key for the associated User.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Navigation property to the associated User.
        /// </summary>
        public User User { get; set; }
    }
}

