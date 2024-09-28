namespace NSWalks.API.Models.Domain
{
	public class Expense
	{
		public Expense()
		{
		}
        public Guid Id { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

