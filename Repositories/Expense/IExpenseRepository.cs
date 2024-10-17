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
		public Task<List<ExpenseDto>> GetExpensesAsync(Pagination pagination);

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

    }
}

