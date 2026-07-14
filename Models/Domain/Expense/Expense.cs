namespace Expense.API.Models.Domain
{
	public class Expense
	{
		public Expense()
		{
		}
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// User the expense is created by.
        /// </summary>
        public Guid CreatedById { get; set; }  // Foreign key for User
        public User CreatedBy { get; set; }     // Navigation property

        /// <summary>
        /// Spending category for dashboard breakdowns (e.g. Groceries, Dining). Null = uncategorised.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Set only at creation. When false, this expense can never have receipt documents
        /// uploaded or scanned - the amount is fully manual.
        /// </summary>
        public bool AllowReceipts { get; set; } = true;

        //Navgational Properties

        /// <summary>
        ///Expense can have one or multiple documents attached.
        /// </summary>
        public ICollection<Document> Documents { get; set; }

        /// <summary>
        /// Navigation property for the many-to-many relationship with User.
        /// </summary>
        public ICollection<ExpenseUser> ExpenseUsers { get; set; }
    }
}

