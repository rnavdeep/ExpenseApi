using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.Repositories.Expense
{
	public class ExpenseRepository: IExpenseRepository
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;

        public ExpenseRepository(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.httpContextAccessor = httpContextAccessor;
		}

        public async Task<ExpenseModel> CreateExpenseAsync(ExpenseModel expense)
        {
            // Retrieve the current logged-in user from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            //check if the user Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Email.Equals(userName)
            );

            if (user != null)
            {
                expense.CreatedById = user.Id;
                await userDocumentsDbContext.Expenses.AddAsync(expense);
                await userDocumentsDbContext.SaveChangesAsync();
            }
            else
            {
                throw new Exception($"Can not create expense for {expense.CreatedById}");
            }

            return expense;

        }

        public async Task<ExpenseUser> CreateExpenseUserAsync(ExpenseUser expenseUser)
        {
            //check if the User Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Id.Equals(expenseUser.UserId));
            //check if the Expense Exists -- Just in case
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(
                expense => expense.Id.Equals(expenseUser.ExpenseId));

            if(user != null && expense != null)
            {
                await userDocumentsDbContext.ExpenseUsers.AddAsync(expenseUser);
                await userDocumentsDbContext.SaveChangesAsync();
            }

            return expenseUser;
        }

        public async Task<ExpenseModel> GetExpenseByIdAsync(Guid expenseId)
        {
            // Retrieve the current logged-in user from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            //check if the user Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Email.Equals(userName)
            );

            if(user != null)
            {
                return await userDocumentsDbContext.Expenses.FirstAsync(expense => expense.Id.Equals(expenseId) && expense.CreatedById.Equals(user.Id));
            }
            throw new Exception($"Expense not found {expenseId} for user {userName}");
        }

        public async Task<List<ExpenseModel>> GetExpensesAsync()
        {
            // Retrieve the current logged-in user's email from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Email.Equals(userName));

            if (user == null)
            {
                // Handle case where the user does not exist
                throw new Exception("Invalid User"); 
            }

            // Retrieve the ExpenseUsers entries related to the current user
            var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                                .Where(eu => eu.UserId == user.Id)
                                .ToListAsync();

            // Initialize a list to store the related expenses
            var expenses = new List<ExpenseModel>();

            // Loop over the list of expenseUsers and fetch each related expense
            foreach (var expenseUser in expenseUsers)
            {
                var expense = await userDocumentsDbContext.Expenses
                                   .FirstOrDefaultAsync(e => e.Id == expenseUser.ExpenseId);

                if (expense != null)
                {
                    expenses.Add(expense);
                }
            }

            // Return the list of expenses
            return expenses;

        }
    }
}

