using System.Globalization;
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

        public async Task<List<ExpenseModel>> GetExpensesSharedAsync(Pagination pagination,FilterBy? filterBy, SortFilter? sortFilter)
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
            var temp = await query.ToListAsync();
            //query = query.Where(q => q.CreatedById.Equals(user.Id));
            var result = await query
                .Where(result => result.CreatedById != user.Id)
                .ToListAsync();
            // Return the list of expenses
            return result;

        }

        public async Task<List<ExpenseModel>> GetExpensesAsync(Pagination pagination, FilterBy? filterBy, SortFilter? sortFilter)
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
                expense.Category = updateExpenseDto.Category;
                await userDocumentsDbContext.SaveChangesAsync();
                return expense;
            }
            throw new Exception("Update Failed, Expense Not found");
        }

        // ----- Dashboard -----

        /// <summary>
        /// Resolve the current user from the JWT NameIdentifier claim (same lookup as GetExpensesAsync).
        /// </summary>
        private async Task<User> GetCurrentUserAsync()
        {
            var userName = httpContextAccessor.HttpContext?.User?.Claims
                             .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var user = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Username.Equals(userName));

            if (user == null)
            {
                throw new Exception("Invalid User");
            }
            return user;
        }

        /// <summary>
        /// Map a period keyword to a [from, to] window. "quarter" = trailing 3 months,
        /// "year" = current calendar year, anything else = current calendar month.
        /// </summary>
        private static (DateTime from, DateTime to) ResolveWindow(string period, DateTime now)
        {
            switch ((period ?? "month").Trim().ToLowerInvariant())
            {
                case "year":
                    return (new DateTime(now.Year, 1, 1), now);
                case "quarter":
                    return (now.AddMonths(-3), now);
                default: // "month"
                    return (new DateTime(now.Year, now.Month, 1), now);
            }
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(string period)
        {
            var user = await GetCurrentUserAsync();
            var now = DateTime.UtcNow;
            var (from, to) = ResolveWindow(period, now);
            var prevFrom = from - (to - from); // previous comparable window

            var myExpensesInWindow = userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == user.Id && e.CreatedAt >= from && e.CreatedAt <= to);

            var totalSpent = await myExpensesInWindow.SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var prevTotal = await userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == user.Id && e.CreatedAt >= prevFrom && e.CreatedAt < from)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var deltaPct = prevTotal == 0m
                ? (totalSpent > 0m ? 100.0 : 0.0)
                : (double)((totalSpent - prevTotal) / prevTotal) * 100.0;

            var receiptsScanned = await userDocumentsDbContext.Documents
                .CountAsync(d => d.UserId == user.Id && d.UploadedAt >= from && d.UploadedAt <= to);

            var youOwe = await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.UserId == user.Id && eu.Expense.CreatedById != user.Id)
                .SumAsync(eu => eu.UserAmount) ?? 0.0;

            var owedToYou = await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.Expense.CreatedById == user.Id && eu.UserId != user.Id)
                .SumAsync(eu => eu.UserAmount) ?? 0.0;

            var categoriesRaw = await myExpensesInWindow
                .GroupBy(e => e.Category)
                .Select(g => new { g.Key, Amount = g.Sum(e => e.Amount) })
                .ToListAsync();

            var categories = categoriesRaw
                .Select(c => new CategoryBreakdownDto { Category = c.Key ?? "Other", Amount = c.Amount })
                .OrderByDescending(c => c.Amount)
                .ToList();

            return new DashboardSummaryDto
            {
                TotalSpent = totalSpent,
                ReceiptsScanned = receiptsScanned,
                YouOwe = youOwe,
                OwedToYou = owedToYou,
                TotalSpentDeltaPct = Math.Round(deltaPct, 1),
                Categories = categories
            };
        }

        public async Task<List<MonthlySpendingDto>> GetMonthlySpendingAsync(int months)
        {
            var user = await GetCurrentUserAsync();
            if (months <= 0) months = 6;

            var now = DateTime.UtcNow;
            var startMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));

            var grouped = await userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == user.Id && e.CreatedAt >= startMonth)
                .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(e => e.Amount) })
                .ToListAsync();

            var result = new List<MonthlySpendingDto>();
            for (int i = 0; i < months; i++)
            {
                var m = startMonth.AddMonths(i);
                var match = grouped.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                result.Add(new MonthlySpendingDto
                {
                    Year = m.Year,
                    Month = m.Month,
                    Label = m.ToString("MMM", CultureInfo.InvariantCulture),
                    Amount = match?.Amount ?? 0m
                });
            }
            return result;
        }

        public async Task<OutstandingBalancesDto> GetOutstandingBalancesAsync()
        {
            var user = await GetCurrentUserAsync();

            // People I owe: my shares on expenses created by others, grouped by the creator.
            var youOweRaw = await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.UserId == user.Id && eu.Expense.CreatedById != user.Id)
                .GroupBy(eu => eu.Expense.CreatedById)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(eu => eu.UserAmount) })
                .ToListAsync();

            // People who owe me: others' shares on expenses I created, grouped by the sharer.
            var owedToYouRaw = await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.Expense.CreatedById == user.Id && eu.UserId != user.Id)
                .GroupBy(eu => eu.UserId)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(eu => eu.UserAmount) })
                .ToListAsync();

            var ids = youOweRaw.Select(x => x.CounterpartyId)
                .Concat(owedToYouRaw.Select(x => x.CounterpartyId))
                .Distinct()
                .ToList();

            var names = await userDocumentsDbContext.Users
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            List<BalanceEntryDto> Project(IEnumerable<dynamic> rows) => rows
                .Select(r => new BalanceEntryDto
                {
                    UserId = r.CounterpartyId,
                    UserName = names.TryGetValue((Guid)r.CounterpartyId, out string n) ? n : "Unknown",
                    Amount = (double)(r.Amount ?? 0.0)
                })
                .OrderByDescending(b => b.Amount)
                .ToList();

            return new OutstandingBalancesDto
            {
                YouOwe = Project(youOweRaw),
                OwedToYou = Project(owedToYouRaw)
            };
        }
    }
}

