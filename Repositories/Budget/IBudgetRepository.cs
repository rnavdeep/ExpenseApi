using Expense.API.Models.DTO;

namespace Expense.API.Repositories.Budget
{
    public interface IBudgetRepository
    {
        /// <summary>
        /// Insert or update the logged in user's budget for the given category (case-insensitive match).
        /// </summary>
        Task<BudgetDto> UpsertAsync(UpsertBudgetDto upsertBudgetDto);

        /// <summary>
        /// The logged in user's budgets joined with their spend for the given period.
        /// </summary>
        Task<List<BudgetStatusDto>> GetStatusAsync(string period);
    }
}
