namespace Expense.API.Models.DTO
{
    public class CategoryExpenseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
