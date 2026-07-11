namespace Expense.API.Models.DTO
{
    public class BudgetDto
    {
        public Guid Id { get; set; }
        public string Category { get; set; }
        public decimal MonthlyLimit { get; set; }
    }
}
