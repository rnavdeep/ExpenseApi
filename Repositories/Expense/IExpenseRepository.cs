using System;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.Repositories.Expense
{
	public interface IExpenseRepository
	{
		/// <summary>
		/// List of expenses of current user 
		/// </summary>
		/// <returns></returns>
		public Task<List<ExpenseModel>> GetExpensesAsync();
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
	}
}

