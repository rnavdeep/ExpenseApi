using System.ComponentModel.DataAnnotations;

namespace Expense.API.Models.DTO
{
    public class CreateSettlementDto
    {
        [Required]
        public Guid PayeeUserId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public string? Note { get; set; }
    }
}
