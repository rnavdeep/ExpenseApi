using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
    public class UpsertCategoryDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public decimal MonthlyLimit { get; set; }
    }
}
