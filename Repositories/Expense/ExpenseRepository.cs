using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;
using BudgetModel = Expense.API.Models.Domain.Budget;
using System;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Notifications;
using Expense.API.Repositories.QueryBuilder;

namespace Expense.API.Repositories.Expense
{
	public class ExpenseRepository: IExpenseRepository
	{
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IServiceProvider serviceProvider;
        private readonly IHubContext<TextractNotificationHub> textractNotification;

        public ExpenseRepository(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor,
            IServiceProvider serviceProvider, IHubContext<TextractNotificationHub> textractNotification)
		{
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.httpContextAccessor = httpContextAccessor;
            this.serviceProvider = serviceProvider;
            this.textractNotification = textractNotification;
		}

        public async Task<ExpenseModel> CreateExpenseAsync(ExpenseModel expense)
        {
            if (expense.Amount < 0)
            {
                throw new Exception("Amount cannot be negative.");
            }

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
                await CheckBudgetThresholdAsync(expense);
            }
            else
            {
                throw new Exception($"Can not create expense for {expense.CreatedById}");
            }

            return expense;

        }

        public async Task<ExpenseUser> CreateExpenseUserAsync(ExpenseUser expenseUser)
        {
            if (await userDocumentsDbContext.LineItems.AnyAsync(li => li.ExpenseId == expenseUser.ExpenseId))
            {
                throw new Exception("This expense's sharing is managed by line-item assignment — use the Document Results page instead.");
            }

            // Check if the User Exists -- Just In Case
            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(
                user => user.Id.Equals(expenseUser.UserId));
            // Check if the Expense Exists -- Just In Case
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(
                expense => expense.Id.Equals(expenseUser.ExpenseId));

            if (user != null && expense != null)
            {
                var currentUser = await GetCurrentUserAsync();
                if (expenseUser.UserId != currentUser.Id)
                {
                    var areFriends = await userDocumentsDbContext.FriendRequests.AnyAsync(
                        fr => ((fr.SentByUserId == currentUser.Id && fr.SentToUserId == expenseUser.UserId)
                               || (fr.SentByUserId == expenseUser.UserId && fr.SentToUserId == currentUser.Id))
                              && fr.IsAccepted == 1);

                    if (!areFriends)
                    {
                        throw new Exception("Users must be friends before sharing an expense.");
                    }
                }

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

            // Update each user with the calculated share percentage and corresponding dollar amount
            foreach (var expUser in expenseUsers)
            {
                expUser.UserShare = eachPercentage;
                expUser.UserAmount = Math.Round(eachPercentage * (double)expense.Amount, 2);
            }
            await userDocumentsDbContext.SaveChangesAsync();

            return expenseUser;
        }

        public async Task<List<ExpenseUser>> RemoveExpenseUserAsync(Guid expenseId, Guid userId)
        {
            if (await userDocumentsDbContext.LineItems.AnyAsync(li => li.ExpenseId == expenseId))
            {
                throw new Exception("This expense's sharing is managed by line-item assignment — use the Document Results page instead.");
            }

            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(
                expense => expense.Id.Equals(expenseId));
            if (expense == null)
            {
                throw new Exception("Expense not found.");
            }

            var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                .Where(expUser => expUser.ExpenseId == expenseId)
                .ToListAsync();

            var expenseUserToRemove = expenseUsers.FirstOrDefault(expUser => expUser.UserId == userId);
            if (expenseUserToRemove == null)
            {
                throw new Exception("User is not assigned to this expense.");
            }

            if (expenseUsers.Count <= 1)
            {
                throw new Exception("Cannot remove the last remaining user from an expense.");
            }

            userDocumentsDbContext.ExpenseUsers.Remove(expenseUserToRemove);
            expenseUsers.Remove(expenseUserToRemove);

            // Re-divide the expense equally among the remaining users.
            var eachPercentage = Math.Round(1.00 / expenseUsers.Count, 2);
            foreach (var expUser in expenseUsers)
            {
                expUser.UserShare = eachPercentage;
                expUser.UserAmount = Math.Round(eachPercentage * (double)expense.Amount, 2);
            }
            await userDocumentsDbContext.SaveChangesAsync();

            return await GetAssignUsers(expenseId);
        }

        public async Task<List<ExpenseUser>> UpdateExpenseUserSharesAsync(Guid expenseId, List<UpdateExpenseUserShareDto> shares)
        {
            if (await userDocumentsDbContext.LineItems.AnyAsync(li => li.ExpenseId == expenseId))
            {
                throw new Exception("This expense's sharing is managed by line-item assignment — use the Document Results page instead.");
            }

            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(
                expense => expense.Id.Equals(expenseId));
            if (expense == null)
            {
                throw new Exception("Expense not found.");
            }

            var expenseUsers = await userDocumentsDbContext.ExpenseUsers
                .Where(expUser => expUser.ExpenseId == expenseId)
                .ToListAsync();

            if (shares == null || shares.Count != expenseUsers.Count
                || !expenseUsers.All(expUser => shares.Any(s => s.UserId == expUser.UserId)))
            {
                throw new Exception("Shares must be provided for exactly the users currently assigned to this expense.");
            }

            var totalShare = shares.Sum(s => s.UserShare);
            if (Math.Round(totalShare, 2) != 1.00)
            {
                throw new Exception("User shares must add up to 100%.");
            }

            foreach (var expUser in expenseUsers)
            {
                var share = shares.First(s => s.UserId == expUser.UserId).UserShare;
                expUser.UserShare = share;
                expUser.UserAmount = Math.Round(share * (double)expense.Amount, 2);
            }
            await userDocumentsDbContext.SaveChangesAsync();

            return await GetAssignUsers(expenseId);
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

                // N-of-M line item assignment counts, left null when the expense has no LineItem rows.
                var lineItems = await userDocumentsDbContext.LineItems
                    .Where(li => li.ExpenseId == expenseId)
                    .Select(li => li.Assignments.Select(a => a.UserId))
                    .ToListAsync();

                if (lineItems.Count > 0)
                {
                    var assignedCounts = lineItems
                        .SelectMany(assigneeIds => assigneeIds)
                        .GroupBy(userId => userId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var expUser in expenseUsers)
                    {
                        expUser.TotalItemsCount = lineItems.Count;
                        expUser.ItemsAssignedCount = assignedCounts.GetValueOrDefault(expUser.UserId);
                    }
                }

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
            var result = await userDocumentsDbContext.DocumentJobResults
                .Include(d => d.LineItems).ThenInclude(li => li.Assignments).ThenInclude(a => a.User)
                .Where(doc => doc.ExpenseId.Equals(expenseId) && doc.DocumentId.Equals(docId))
                .FirstOrDefaultAsync();
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
            var query =  queryBuilder.BuildQuery(pagination, filters, sortFilter).Include(e => e.CreatedBy);
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

                if (updateExpenseDto.Amount.HasValue)
                {
                    if (updateExpenseDto.Amount.Value < 0)
                    {
                        throw new Exception("Amount cannot be negative.");
                    }

                    var scannedReceiptsTotal = await userDocumentsDbContext.DocumentJobResults
                        .Where(r => r.ExpenseId == expense.Id && r.Status == 1)
                        .SumAsync(r => (decimal?)r.Total) ?? 0m;

                    if (updateExpenseDto.Amount.Value < scannedReceiptsTotal)
                    {
                        throw new Exception($"Amount cannot be less than the total of scanned receipts (${scannedReceiptsTotal}).");
                    }

                    expense.Amount = updateExpenseDto.Amount.Value;
                }

                await userDocumentsDbContext.SaveChangesAsync();
                await CheckBudgetThresholdAsync(expense);
                return expense;
            }
            throw new Exception("Update Failed, Expense Not found");
        }

        public async Task<Dictionary<Guid, decimal>> GetScannedReceiptsTotalsAsync(IEnumerable<Guid> expenseIds)
        {
            var ids = expenseIds.ToList();
            return await userDocumentsDbContext.DocumentJobResults
                .Where(r => ids.Contains(r.ExpenseId) && r.Status == 1)
                .GroupBy(r => r.ExpenseId)
                .Select(g => new { ExpenseId = g.Key, Total = g.Sum(r => r.Total) })
                .ToDictionaryAsync(x => x.ExpenseId, x => x.Total);
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

            var netBalances = await GetNetBalancesByCounterpartyAsync(user.Id);
            var youOwe = netBalances.Values.Where(v => v < 0).Sum(v => -v);
            var owedToYou = netBalances.Values.Where(v => v > 0).Sum(v => v);

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
            var netBalances = await GetNetBalancesByCounterpartyAsync(user.Id);

            var names = await userDocumentsDbContext.Users
                .Where(u => netBalances.Keys.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            string NameOf(Guid counterpartyId) => names.TryGetValue(counterpartyId, out var n) ? n : "Unknown";

            var youOwe = netBalances
                .Where(kv => kv.Value < 0)
                .Select(kv => new BalanceEntryDto { UserId = kv.Key, UserName = NameOf(kv.Key), Amount = -kv.Value })
                .OrderByDescending(b => b.Amount)
                .ToList();

            var owedToYou = netBalances
                .Where(kv => kv.Value > 0)
                .Select(kv => new BalanceEntryDto { UserId = kv.Key, UserName = NameOf(kv.Key), Amount = kv.Value })
                .OrderByDescending(b => b.Amount)
                .ToList();

            return new OutstandingBalancesDto
            {
                YouOwe = youOwe,
                OwedToYou = owedToYou
            };
        }

        /// <summary>
        /// Net amount owed with every counterparty the user has an expense share or settlement with:
        /// positive = counterparty owes the user, negative = the user owes the counterparty, 0 = settled.
        /// Nets expense shares against settlements paid in either direction.
        /// </summary>
        private async Task<Dictionary<Guid, double>> GetNetBalancesByCounterpartyAsync(Guid userId)
        {
            // My shares on expenses created by others, grouped by the creator (money I owe from expenses).
            var oweFromExpenses = (await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.UserId == userId && eu.Expense.CreatedById != userId)
                .GroupBy(eu => eu.Expense.CreatedById)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(eu => eu.UserAmount) ?? 0.0 })
                .ToListAsync())
                .ToDictionary(x => x.CounterpartyId, x => x.Amount);

            // Others' shares on expenses I created, grouped by the sharer (money owed to me from expenses).
            var owedFromExpenses = (await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.Expense.CreatedById == userId && eu.UserId != userId)
                .GroupBy(eu => eu.UserId)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(eu => eu.UserAmount) ?? 0.0 })
                .ToListAsync())
                .ToDictionary(x => x.CounterpartyId, x => x.Amount);

            // Settlements I paid to each counterparty (reduces what I owe them).
            var settlementsIPaid = (await userDocumentsDbContext.Settlements
                .Where(s => s.PayerId == userId)
                .GroupBy(s => s.PayeeId)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(s => s.Amount) })
                .ToListAsync())
                .ToDictionary(x => x.CounterpartyId, x => (double)x.Amount);

            // Settlements each counterparty paid me (reduces what they owe me).
            var settlementsTheyPaid = (await userDocumentsDbContext.Settlements
                .Where(s => s.PayeeId == userId)
                .GroupBy(s => s.PayerId)
                .Select(g => new { CounterpartyId = g.Key, Amount = g.Sum(s => s.Amount) })
                .ToListAsync())
                .ToDictionary(x => x.CounterpartyId, x => (double)x.Amount);

            var counterpartyIds = oweFromExpenses.Keys
                .Concat(owedFromExpenses.Keys)
                .Concat(settlementsIPaid.Keys)
                .Concat(settlementsTheyPaid.Keys)
                .Distinct();

            var net = new Dictionary<Guid, double>();
            foreach (var counterpartyId in counterpartyIds)
            {
                var owed = owedFromExpenses.GetValueOrDefault(counterpartyId)
                    - settlementsTheyPaid.GetValueOrDefault(counterpartyId);
                var owe = oweFromExpenses.GetValueOrDefault(counterpartyId)
                    - settlementsIPaid.GetValueOrDefault(counterpartyId);
                net[counterpartyId] = owed - owe;
            }
            return net;
        }

        public async Task<BalanceDetailDto?> GetBalanceDetailAsync(Guid counterpartyId)
        {
            var user = await GetCurrentUserAsync();

            var counterparty = await userDocumentsDbContext.Users.FirstOrDefaultAsync(u => u.Id == counterpartyId);
            if (counterparty == null)
            {
                return null;
            }

            var expenseEntries = await userDocumentsDbContext.ExpenseUsers
                .Where(eu =>
                    (eu.UserId == user.Id && eu.Expense.CreatedById == counterpartyId) ||
                    (eu.UserId == counterpartyId && eu.Expense.CreatedById == user.Id))
                .Select(eu => new BalanceDetailEntryDto
                {
                    Type = "expense",
                    Id = eu.ExpenseId,
                    Description = eu.Expense.Title,
                    Amount = eu.UserAmount ?? 0.0,
                    Direction = eu.UserId == user.Id ? "youOwe" : "owedToYou",
                    CreatedAt = eu.Expense.CreatedAt
                })
                .ToListAsync();

            var settlementEntries = await userDocumentsDbContext.Settlements
                .Where(s =>
                    (s.PayerId == user.Id && s.PayeeId == counterpartyId) ||
                    (s.PayerId == counterpartyId && s.PayeeId == user.Id))
                .Select(s => new BalanceDetailEntryDto
                {
                    Type = "settlement",
                    Id = s.Id,
                    Description = s.Note ?? "Settlement",
                    Amount = (double)s.Amount,
                    Direction = s.PayerId == user.Id ? "youOwe" : "owedToYou",
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            var entries = expenseEntries.Concat(settlementEntries)
                .OrderBy(e => e.CreatedAt)
                .ToList();

            var netBalances = await GetNetBalancesByCounterpartyAsync(user.Id);
            var net = netBalances.GetValueOrDefault(counterpartyId);

            return new BalanceDetailDto
            {
                UserId = counterparty.Id,
                UserName = counterparty.Username,
                NetAmount = Math.Abs(net),
                Direction = net > 0 ? "owedToYou" : net < 0 ? "youOwe" : "settled",
                Entries = entries
            };
        }

        // ----- Budget threshold alerts -----

        private static string NormalizeCategory(string? category)
        {
            var trimmed = category?.Trim();
            return string.IsNullOrEmpty(trimmed) ? "Other" : trimmed;
        }

        /// <summary>
        /// Sum of the user's current-calendar-month expenses in the given category (same "Other"
        /// bucket semantics as GetDashboardSummaryAsync/BudgetRepository), excluding one expense.
        /// </summary>
        private async Task<decimal> GetCurrentMonthCategorySpentAsync(Guid userId, string normalizedCategory, Guid excludeExpenseId)
        {
            var now = DateTime.UtcNow;
            var from = new DateTime(now.Year, now.Month, 1);

            var expenses = await userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == userId && e.CreatedAt >= from && e.CreatedAt <= now && e.Id != excludeExpenseId)
                .Select(e => new { e.Category, e.Amount })
                .ToListAsync();

            return expenses
                .Where(e => NormalizeCategory(e.Category).Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
                .Sum(e => e.Amount);
        }

        /// <summary>
        /// After an expense create/update, alert the owner once per budget/threshold/month when the
        /// expense's category utilization crosses 80% or 100%. A notification failure must not fail
        /// the expense write.
        /// </summary>
        private async Task CheckBudgetThresholdAsync(ExpenseModel expense)
        {
            try
            {
                var normalizedCategory = NormalizeCategory(expense.Category);

                var budget = await userDocumentsDbContext.Budgets.FirstOrDefaultAsync(
                    b => b.UserId == expense.CreatedById && b.Category.ToLower() == normalizedCategory.ToLower());

                if (budget == null || budget.MonthlyLimit <= 0)
                {
                    return;
                }

                var spentExcludingThis = await GetCurrentMonthCategorySpentAsync(expense.CreatedById, normalizedCategory, expense.Id);
                var spentIncludingThis = spentExcludingThis + expense.Amount;

                var beforePct = (double)(spentExcludingThis / budget.MonthlyLimit) * 100.0;
                var afterPct = (double)(spentIncludingThis / budget.MonthlyLimit) * 100.0;

                foreach (var threshold in new[] { 80.0, 100.0 })
                {
                    if (beforePct < threshold && afterPct >= threshold)
                    {
                        await NotifyBudgetThresholdAsync(expense.CreatedById, budget, afterPct);
                    }
                }
            }
            catch
            {
                // Swallow: a notification failure must not fail the expense write.
            }
        }

        private async Task NotifyBudgetThresholdAsync(Guid userId, BudgetModel budget, double afterPct)
        {
            var pctRounded = (int)Math.Round(afterPct, MidpointRounding.AwayFromZero);
            var message = $"You've used {pctRounded}% of your ${budget.MonthlyLimit:0.##} {budget.Category} budget this month";

            using (var scope = serviceProvider.CreateScope())
            {
                var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                await textractNotificationDb.CreateNotifcation(userId, message, "Budget alert", 0);
            }

            var user = await userDocumentsDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                await textractNotification.Clients.User(user.Username).SendAsync("TextractNotification", message);
            }
        }

        // ----- Line-item assignment -----

        /// <summary>
        /// Throws when userId isn't the caller and has no accepted FriendRequest with the caller (same
        /// predicate as CreateExpenseUserAsync's inline friendship check).
        /// </summary>
        private async Task EnsureFriendsWithCallerAsync(Guid callerId, Guid userId)
        {
            if (userId == callerId)
            {
                return;
            }

            var areFriends = await userDocumentsDbContext.FriendRequests.AnyAsync(
                fr => ((fr.SentByUserId == callerId && fr.SentToUserId == userId)
                       || (fr.SentByUserId == userId && fr.SentToUserId == callerId))
                      && fr.IsAccepted == 1);

            if (!areFriends)
            {
                throw new Exception("Users must be friends before sharing an expense.");
            }
        }

        private Task<LineItem> GetLineItemWithAssigneesAsync(Guid lineItemId)
        {
            return userDocumentsDbContext.LineItems
                .Include(li => li.Assignments).ThenInclude(a => a.User)
                .FirstAsync(li => li.Id == lineItemId);
        }

        public async Task<LineItem> AssignUserToLineItemAsync(Guid lineItemId, Guid userId)
        {
            var lineItem = await userDocumentsDbContext.LineItems
                .Include(li => li.Assignments)
                .FirstOrDefaultAsync(li => li.Id == lineItemId);
            if (lineItem == null)
            {
                throw new Exception("Line item not found.");
            }

            var currentUser = await GetCurrentUserAsync();
            await EnsureFriendsWithCallerAsync(currentUser.Id, userId);

            if (!lineItem.Assignments.Any(a => a.UserId == userId))
            {
                await userDocumentsDbContext.LineItemAssignments.AddAsync(new LineItemAssignment
                {
                    LineItemId = lineItemId,
                    UserId = userId
                });
                await userDocumentsDbContext.SaveChangesAsync();
                await RecomputeExpenseUsersFromAssignmentsAsync(lineItem.ExpenseId);
            }

            return await GetLineItemWithAssigneesAsync(lineItemId);
        }

        public async Task<LineItem> RemoveUserFromLineItemAsync(Guid lineItemId, Guid userId)
        {
            var lineItem = await userDocumentsDbContext.LineItems
                .Include(li => li.Assignments)
                .FirstOrDefaultAsync(li => li.Id == lineItemId);
            if (lineItem == null)
            {
                throw new Exception("Line item not found.");
            }

            var assignment = lineItem.Assignments.FirstOrDefault(a => a.UserId == userId);
            if (assignment == null)
            {
                throw new Exception("User is not assigned to this line item.");
            }

            if (lineItem.Assignments.Count <= 1)
            {
                throw new Exception("Cannot remove the last remaining assignee from a line item.");
            }

            userDocumentsDbContext.LineItemAssignments.Remove(assignment);
            await userDocumentsDbContext.SaveChangesAsync();
            await RecomputeExpenseUsersFromAssignmentsAsync(lineItem.ExpenseId);

            return await GetLineItemWithAssigneesAsync(lineItemId);
        }

        public async Task<List<LineItem>> AssignUserToAllLineItemsAsync(Guid expenseId, Guid userId)
        {
            var lineItems = await userDocumentsDbContext.LineItems
                .Where(li => li.ExpenseId == expenseId)
                .Include(li => li.Assignments)
                .ToListAsync();

            var currentUser = await GetCurrentUserAsync();
            await EnsureFriendsWithCallerAsync(currentUser.Id, userId);

            var itemsMissingUser = lineItems.Where(li => !li.Assignments.Any(a => a.UserId == userId)).ToList();
            if (itemsMissingUser.Count > 0)
            {
                foreach (var lineItem in itemsMissingUser)
                {
                    await userDocumentsDbContext.LineItemAssignments.AddAsync(new LineItemAssignment
                    {
                        LineItemId = lineItem.Id,
                        UserId = userId
                    });
                }
                await userDocumentsDbContext.SaveChangesAsync();
                await RecomputeExpenseUsersFromAssignmentsAsync(expenseId);
            }

            return await userDocumentsDbContext.LineItems
                .Where(li => li.ExpenseId == expenseId)
                .Include(li => li.Assignments).ThenInclude(a => a.User)
                .ToListAsync();
        }

        public async Task RecomputeExpenseUsersFromAssignmentsAsync(Guid expenseId)
        {
            var expense = await userDocumentsDbContext.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId);
            if (expense == null)
            {
                return;
            }

            var lineItems = await userDocumentsDbContext.LineItems
                .Where(li => li.ExpenseId == expenseId)
                .Include(li => li.Assignments)
                .ToListAsync();

            // Per-person totals: sum of that person's even split across every line item they're on. Any
            // remainder between expense.Amount and the itemized sum (tax/tip/manual top-ups) is left
            // unassigned, never redistributed.
            var perUserDollar = new Dictionary<Guid, double>();
            foreach (var lineItem in lineItems)
            {
                if (lineItem.Amount == null || lineItem.Assignments == null || lineItem.Assignments.Count == 0)
                {
                    continue;
                }

                var perAssigneeShare = (double)lineItem.Amount.Value / lineItem.Assignments.Count;
                foreach (var assignment in lineItem.Assignments)
                {
                    perUserDollar[assignment.UserId] = perUserDollar.GetValueOrDefault(assignment.UserId) + perAssigneeShare;
                }
            }

            var existingExpenseUsers = await userDocumentsDbContext.ExpenseUsers
                .Where(eu => eu.ExpenseId == expenseId)
                .ToListAsync();

            foreach (var (assignedUserId, dollarAmount) in perUserDollar)
            {
                var userAmount = Math.Round(dollarAmount, 2);
                var userShare = expense.Amount > 0 ? dollarAmount / (double)expense.Amount : 0;

                var existingExpenseUser = existingExpenseUsers.FirstOrDefault(eu => eu.UserId == assignedUserId);
                if (existingExpenseUser != null)
                {
                    existingExpenseUser.UserAmount = userAmount;
                    existingExpenseUser.UserShare = userShare;
                }
                else
                {
                    await userDocumentsDbContext.ExpenseUsers.AddAsync(
                        new ExpenseUser(expenseId, assignedUserId, userAmount) { UserShare = userShare });
                }
            }

            var expenseUsersToRemove = existingExpenseUsers
                .Where(eu => !perUserDollar.ContainsKey(eu.UserId))
                .ToList();
            if (expenseUsersToRemove.Count > 0)
            {
                userDocumentsDbContext.ExpenseUsers.RemoveRange(expenseUsersToRemove);
            }

            await userDocumentsDbContext.SaveChangesAsync();
        }
    }
}

