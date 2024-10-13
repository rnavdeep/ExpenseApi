using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;
using System;
using Expense.API.Models.DTO;

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

        public async Task<List<ExpenseModel>> GetExpensesAsync()
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

            // list of expenses --- to apply pagination/filter by for search in future
            List<ExpenseModel> expenses = await userDocumentsDbContext.Expenses
                .Where(expense => expense.CreatedById == user.Id)
                .GroupJoin(userDocumentsDbContext.Documents,
                           expense => expense.Id,
                           document => document.ExpenseId,
                           (expense, documents) => new ExpenseModel
                           {
                               Id = expense.Id,
                               Amount = expense.Amount,
                               Documents = documents.Select(document => new Document
                               {
                                   Id = document.Id,
                                   S3Url = document.S3Url,
                                   FileName = document.FileName
                               }).ToList(),
                               CreatedAt = expense.CreatedAt,
                               Title = expense.Title,
                               Description = expense.Description
                           })
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

