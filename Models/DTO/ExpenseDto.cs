using System;
namespace Expense.API.Models.DTO
{
	public class ExpenseDto:AddExpenseDto
	{
		public ExpenseDto()
		{
		}
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedById { get; set; }
        public string CreatedByName { get; set; } 
        public List<string> DocumentUrls { get; set; }
    }
}

