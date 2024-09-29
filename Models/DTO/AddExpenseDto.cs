using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
	public class AddExpenseDto
	{
		public AddExpenseDto()
		{
		}
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }
}

