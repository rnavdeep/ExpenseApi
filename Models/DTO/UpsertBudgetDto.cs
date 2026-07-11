using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
    public class UpsertBudgetDto
    {
        [Required]
        public string Category { get; set; }

        [Required]
        public decimal MonthlyLimit { get; set; }
    }
}
