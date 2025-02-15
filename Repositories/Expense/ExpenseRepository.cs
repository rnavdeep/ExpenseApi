using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;
using System;
using Expense.API.Models.DTO;
using Expense.API.Repositories.QueryBuilder;

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
                             .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            //check if the user Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Username.Equals(userName)
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
            // Check if the User Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Id.Equals(expenseUser.UserId));
            // Check if the Expense Exists -- Just In Case
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(
                expense => expense.Id.Equals(expenseUser.ExpenseId));

            if (user != null && expense != null)
            {
                var expenseUserExists = await userDocumentsDbContext.ExpenseUsers.FirstOrDefaultAsync(
                                            expenseUser => expenseUser.ExpenseId == expense.Id
                                            && expenseUser.UserId == user.Id);
                if(expenseUserExists != null)
                {
                    throw new Exception("User has already been assigned to the Expense.");
                }

                await userDocumentsDbContext.ExpenseUsers.AddAsync(expenseUser);
                await userDocumentsDbContext.SaveChangesAsync();
            }

            // Update user share - based on the number of users the expense is shared with. Expense is shared equally.
            var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                .Where(expUser => expUser.ExpenseId == expenseUser.ExpenseId)
                .ToListAsync();

            var eachPercentage = Math.Round(1.00 / expenseUsers.Count, 2);

            // Update each user with the calculated share percentage
            foreach (var expUser in expenseUsers)
            {
                expUser.UserShare = eachPercentage;
            }
            await userDocumentsDbContext.SaveChangesAsync();

            return expenseUser;
        }

        public async Task<List<ExpenseUser>> GetAssignUsers(Guid expenseId)
        {
            try
            {
                var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                    .Where(expUser => expUser.ExpenseId == expenseId)
                    .Join(
                        userDocumentsDbContext.Users,
                        expUser => expUser.UserId,
                        user => user.Id,
                        (expUser, user) => new ExpenseUser(
                            expUser.ExpenseId,
                            user.Id,
                            expUser.UserAmount
                        )
                        {
                            UserShare = expUser.UserShare,
                            User = user
                        })
                    .ToListAsync();

                return expenseUsers;
            }
            catch (Exception e)
            {
                throw new Exception($"Error fetching assigned users: {e.Message}", e);
            }
        }



        public async Task<List<UploadedDocumentDto>> GetDocByExpenseId(Guid expenseId)
        {
            var result = await userDocumentsDbContext.Documents
                .GroupJoin(
                    userDocumentsDbContext.DocumentJobResults,
                    doc => doc.Id,                     
                    jobResult => jobResult.DocumentId,
                    (doc, jobResults) => new { doc, jobResults }
                )
                .SelectMany(
                    x => x.jobResults.DefaultIfEmpty(), 
                    (x, jobResult) => new { x.doc, jobResult } 
                )
                .Where(x => x.doc.ExpenseId.Equals(expenseId)) 
                .Select(result => new UploadedDocumentDto
                {
                    Id = result.doc.Id.ToString(), 
                    Name = result.doc.FileName,    
                    Url = result.doc.S3Url,       
                    JobStatus = result.jobResult != null ? result.jobResult.Status : null 
                }).Where(result => result.JobStatus != 3)
                .ToListAsync();


            return result;
        }

        public async Task<DocumentJobResult?> GetDocResult(Guid expenseId, Guid docId)
        {
            var result =  await userDocumentsDbContext.DocumentJobResults.Where(doc => doc.ExpenseId.Equals(expenseId) && doc.DocumentId.Equals(docId)).FirstOrDefaultAsync();
            return result;
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

        public async Task<List<ExpenseModel>> GetExpensesAsync(Pagination pagination,FilterBy? filterBy, SortFilter? sortFilter)
        {
            // Retrieve the current logged-in user's email from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Username.Equals(userName));

            if (user == null)
            {
                // Handle case where the user does not exist -- can not return expenses
                throw new Exception("Invalid User"); 
            }
            var sharedExpenses =await userDocumentsDbContext.ExpenseUsers.Where(eu => eu.UserId.Equals(user.Id)).ToListAsync();
            var queryBuilder = new QueryBuilder<ExpenseModel>(userDocumentsDbContext);
            List<FilterBy> filters = new List<FilterBy>();
            filters.Add(new FilterBy { PropertyName = "Id", Type = "in", Value = sharedExpenses });
            if (filterBy != null)
            {
                filters.Add(filterBy);
            }
            var query =  queryBuilder.BuildQuery(pagination, filters, sortFilter);
            //query = query.Where(q => q.CreatedById.Equals(user.Id));
            var result = await query.ToListAsync();

            // Return the list of expenses
            return result;

        }

        public async Task<List<ExpenseModel>> GetExpensesSharedAsync(Pagination pagination, FilterBy? filterBy, SortFilter? sortFilter)
        {
            // Retrieve the current logged-in user's email from the HttpContext
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Username.Equals(userName));

            if (user == null)
            {
                // Handle case where the user does not exist -- can not return expenses
                throw new Exception("Invalid User");
            }
            var queryBuilder = new QueryBuilder<ExpenseModel>(userDocumentsDbContext);
            List<FilterBy> filters = new List<FilterBy>();
            filters.Add(new FilterBy { PropertyName = "CreatedById", Type = "==", Value = user.Id.ToString() });
            if (filterBy != null)
            {
                filters.Add(filterBy);
            }
            var query = queryBuilder.BuildQuery(pagination, filters, sortFilter);
            //query = query.Where(q => q.CreatedById.Equals(user.Id));
            var result = await query.ToListAsync();

            // Return the list of expenses
            return result;
        }
        public async Task<int> GetExpensesCountAsync()
        {
            // Retrieve the current logged-in user's email from the HttpContext -- email is always unique
            var emailUser = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Email.Equals(emailUser));

            if (user == null)
            {
                // Handle case where the user does not exist -- can not return expenses
                throw new Exception("Invalid User");
            }
            return await userDocumentsDbContext.Expenses.CountAsync();
        }

        public async Task<List<ExpenseDto>> GetExpensesDropdownAsync()
        {
            // Retrieve the current logged-in user's email from the HttpContext -- email is always unique
            var emailUser = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            // Check if the user exists in the database
            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Email.Equals(emailUser));

            if (user == null)
            {
                // Handle case where the user does not exist -- can not return expenses
                throw new Exception("Invalid User");
            }

            List<ExpenseDto> expenses = await userDocumentsDbContext.Expenses
                .Where(expense => expense.CreatedById == user.Id)
                .Join(userDocumentsDbContext.DocumentJobResults,
                           expense => expense.Id,
                           document => document.ExpenseId,
                           (expense, documents) => new ExpenseDto
                           {
                               Id = expense.Id.ToString(),
                               Amount = expense.Amount,
                               CreatedAt = expense.CreatedAt.ToShortDateString(),
                               Title = expense.Title,
                               Description = expense.Description
                           })
                .GroupBy(expense=>expense.Id)
                .Select(group => group.First())
                .ToListAsync();

            // Return the list of expenses
            return expenses;
        }

        public async Task<Boolean> RemoveExpense(Guid id)
        {
            // Find the expense
            var expense = await userDocumentsDbContext.Expenses.FindAsync(id); 

            if (expense != null)
            {
                userDocumentsDbContext.Expenses.Remove(expense);
                await userDocumentsDbContext.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<ExpenseModel> UpdateExpenseAsync(UpdateExpenseDto updateExpenseDto)
        {
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(exp=>exp.Id.ToString().Equals(updateExpenseDto.Id));

            if (expense != null)
            {

                expense.Title = updateExpenseDto.Title;
                expense.Description = updateExpenseDto.Description;
                await userDocumentsDbContext.SaveChangesAsync();
                return expense;
            }
            throw new Exception("Update Failed, Expense Not found");
        }
    }
}

