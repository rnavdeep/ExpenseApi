using System;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.Repositories.Expense
{
	public interface IExpenseRepository
	{
		/// <summary>
		/// List of expenses of current user 
		/// </summary>
		/// <returns></returns>
		public Task<List<ExpenseModel>> GetExpensesAsync(Pagination pagination, FilterBy? filterBy, SortFilter? sortFilter);

        /// <summary>
        /// List of expenses shared with current user 
        /// </summary>
        /// <returns></returns>
        public Task<List<ExpenseModel>> GetExpensesSharedAsync(Pagination pagination, FilterBy? filterBy, SortFilter? sortFilter);

        /// <summary>
        /// List of expenses of current user 
        /// </summary>
        /// <returns></returns>
        public Task<List<ExpenseDto>> GetExpensesDropdownAsync();

        /// <summary>
        /// Count of expenses of current user
        /// </summary>
        /// <returns></returns>
        public Task<int> GetExpensesCountAsync();

        /// <summary>
        /// Get by Expense Id
        /// </summary>
        /// <param name="expenseId"></param>
        /// <returns></returns>
        public Task<ExpenseModel> GetExpenseByIdAsync(Guid expenseId);

        /// <summary>
        /// Get Doc Data by Expense Id
        /// </summary>
        /// <param name="expenseId"></param>
        /// <returns></returns>
        public Task<List<UploadedDocumentDto>> GetDocByExpenseId(Guid expenseId);

        /// <summary>
        /// Create Expense for logged in User.
        /// </summary>
        /// <param name="addExpense"></param>
        /// <returns></returns>
        public Task<ExpenseModel> CreateExpenseAsync(ExpenseModel expense);

		/// <summary>
		/// Add in user-expense linking table
		/// </summary>
		/// <param name="expenseUser"></param>
		/// <returns></returns>
		public Task<ExpenseUser> CreateExpenseUserAsync(ExpenseUser expenseUser);

        /// <summary>
        /// Remove a user from an expense and re-divide the remaining share equally.
        /// </summary>
        /// <param name="expenseId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public Task<List<ExpenseUser>> RemoveExpenseUserAsync(Guid expenseId, Guid userId);

        /// <summary>
        /// Set custom share percentages for every user assigned to an expense. The provided shares
        /// must cover exactly the currently assigned users and sum to 100%.
        /// </summary>
        /// <param name="expenseId"></param>
        /// <param name="shares"></param>
        /// <returns></returns>
        public Task<List<ExpenseUser>> UpdateExpenseUserSharesAsync(Guid expenseId, List<UpdateExpenseUserShareDto> shares);

        /// <summary>
        /// Remove expense
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<Boolean> RemoveExpense(Guid id);

        /// <summary>
        /// Update expense 
        /// </summary>
        /// <param name="updateExpenseDto"></param>
        /// <returns></returns>
        public Task<ExpenseModel> UpdateExpenseAsync(UpdateExpenseDto updateExpenseDto);

        /// <summary>
        /// Get textract result for the table
        /// </summary>
        /// <param name="expenseId"></param>
        /// <param name="docId"></param>
        /// <returns></returns>
        public Task<DocumentJobResult?> GetDocResult(Guid expenseId, Guid docId);

        /// <summary>
        /// Get Assign Users
        /// </summary>
        /// <param name="expenseId"></param>
        /// <returns></returns>
        public Task<List<ExpenseUser>> GetAssignUsers(Guid expenseId);

        /// <summary>
        /// Dashboard KPI strip + category breakdown for the current user, scoped to a period
        /// ("month" | "quarter" | "year").
        /// </summary>
        public Task<DashboardSummaryDto> GetDashboardSummaryAsync(string period);

        /// <summary>
        /// Dashboard bar chart: SUM(Amount) per month for the current user over the last N months
        /// (gap months filled with 0).
        /// </summary>
        public Task<List<MonthlySpendingDto>> GetMonthlySpendingAsync(int months);

        /// <summary>
        /// Dashboard outstanding balances: amounts owed per counterparty (net of settlements), split
        /// by direction.
        /// </summary>
        public Task<OutstandingBalancesDto> GetOutstandingBalancesAsync();

        /// <summary>
        /// Net balance with a single counterparty plus the chronological ledger (expense shares +
        /// settlements) between the caller and that counterparty. Null when counterpartyId is not an
        /// existing user.
        /// </summary>
        public Task<BalanceDetailDto?> GetBalanceDetailAsync(Guid counterpartyId);

    }
}

