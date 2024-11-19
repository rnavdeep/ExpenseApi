namespace Expense.API.Models.Domain
{
	public class ExpenseUser
	{
		public ExpenseUser(Guid expenseId, Guid userId, double? userAmount)
		{
            this.ExpenseId = expenseId;
            this.UserId = userId;
            this.UserAmount = userAmount;
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

        /// <summary>
        /// User Share value b/w 0-1
        /// </summary>
        public double UserShare { get; set; }

        /// <summary>
        /// User Share Amount
        /// </summary>
        public double? UserAmount { get; set; }
    }
}

