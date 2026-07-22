using Expense.API.Models.DTO;

namespace Expense.API.Repositories.Category
{
    public interface ICategoryRepository
    {
        /// <summary>
        /// Insert or update the logged in user's category (case-insensitive name match).
        /// </summary>
        Task<CategoryDto> UpsertAsync(UpsertCategoryDto upsertCategoryDto);

        /// <summary>
        /// The logged in user's categories joined with their spend for the given period.
        /// </summary>
        Task<List<CategoryStatusDto>> GetStatusAsync(string period);

        /// <summary>
        /// Delete the logged in user's category by id. Returns false if it doesn't exist or isn't owned by the caller.
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Up to `take` most recent expenses this month in the given category (same "Other"
        /// normalization as GetStatusAsync), for previewing before creating/editing a category.
        /// </summary>
        Task<List<CategoryExpenseDto>> GetCategoryExpensesAsync(string name, int take = 5);
    }
}
