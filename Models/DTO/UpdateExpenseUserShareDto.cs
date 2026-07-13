using System;
namespace Expense.API.Models.DTO
{
	public class UpdateExpenseUserShareDto
	{
        /// <summary>
        /// The user whose share is being set.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// User Share value between 0 and 1.
        /// </summary>
        public double UserShare { get; set; }
    }
}
