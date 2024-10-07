using System;
using Expense.API.Models.Domain;

namespace Expense.API.Models.DTO
{
	public class ExpenseDto:AddExpenseDto
	{
		public ExpenseDto()
		{
		}
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string CreatedAt { get; set; }
        //public ICollection<Document> Documents { get; set; }
        //public List<string> UserIds { get; set; }
    }
}

