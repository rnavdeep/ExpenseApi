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
        public decimal ScannedReceiptsTotal { get; set; }
        /// <summary>
        /// Username of the expense owner. Populated only when the CreatedBy
        /// navigation is included by the query (e.g. shared-expenses listing).
        /// </summary>
        public string? SharedByUsername { get; set; }
        //public ICollection<Document> Documents { get; set; }
        //public List<string> UserIds { get; set; }
    }
}

