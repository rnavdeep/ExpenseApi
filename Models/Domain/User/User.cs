namespace Expense.API.Models.Domain
{
    public class User
	{
		public User()
		{
		}
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property representing the many-to-many relationship with Expense.
        /// </summary>
        public ICollection<ExpenseUser> ExpenseUsers { get; set; }

    }
}

