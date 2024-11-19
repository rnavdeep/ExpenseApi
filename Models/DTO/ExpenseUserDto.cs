using System;
namespace Expense.API.Models.DTO
{
	public class ExpenseUserDto
	{
		public ExpenseUserDto()
		{
		}
        /// <summary>
        /// Foreign key for the associated Expense.
        /// </summary>
        public Guid ExpenseId { get; set; }

        /// <summary>
        /// Foreign key for the associated User.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// User Share value between 0 and 1.
        /// </summary>
        public double UserShare { get; set; }

        /// <summary>
        /// User Share Amount.
        /// </summary>
        public double? UserAmount { get; set; }

        /// <summary>
        /// Name or details of the associated User.
        /// </summary>
        public string UserName { get; set; }
    }
}

