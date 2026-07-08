namespace Expense.API.Models.DTO
{
    public class SettlementDto
    {
        public Guid Id { get; set; }
        public Guid PayerUserId { get; set; }
        public string PayerUserName { get; set; }
        public Guid PayeeUserId { get; set; }
        public string PayeeUserName { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
