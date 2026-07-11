using System.Security.Claims;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using BudgetModel = Expense.API.Models.Domain.Budget;

namespace Expense.API.Repositories.Budget
{
    public class BudgetRepository : IBudgetRepository
    {
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMapper mapper;

        public BudgetRepository(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor, IMapper mapper)
        {
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.httpContextAccessor = httpContextAccessor;
            this.mapper = mapper;
        }

        /// <summary>
        /// Resolve the current user from the JWT NameIdentifier claim (same lookup as ExpenseRepository).
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

        public async Task<BudgetDto> UpsertAsync(UpsertBudgetDto upsertBudgetDto)
        {
            if (upsertBudgetDto.MonthlyLimit <= 0)
            {
                throw new Exception("Monthly limit must be greater than zero.");
            }

            var category = upsertBudgetDto.Category?.Trim();
            if (string.IsNullOrEmpty(category))
            {
                throw new Exception("Category can not be blank.");
            }

            var user = await GetCurrentUserAsync();

            var budget = await userDocumentsDbContext.Budgets
                .FirstOrDefaultAsync(b => b.UserId == user.Id && b.Category.ToLower() == category.ToLower());

            if (budget == null)
            {
                budget = new BudgetModel
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Category = category,
                    MonthlyLimit = upsertBudgetDto.MonthlyLimit,
                    UpdatedAt = DateTime.UtcNow
                };
                await userDocumentsDbContext.Budgets.AddAsync(budget);
            }
            else
            {
                budget.MonthlyLimit = upsertBudgetDto.MonthlyLimit;
                budget.UpdatedAt = DateTime.UtcNow;
            }

            await userDocumentsDbContext.SaveChangesAsync();

            return mapper.Map<BudgetDto>(budget);
        }

        public async Task<List<BudgetStatusDto>> GetStatusAsync(string period)
        {
            var user = await GetCurrentUserAsync();

            var budgets = await userDocumentsDbContext.Budgets
                .Where(b => b.UserId == user.Id)
                .ToListAsync();

            if (!budgets.Any())
            {
                return new List<BudgetStatusDto>();
            }

            var now = DateTime.UtcNow;
            var from = new DateTime(now.Year, now.Month, 1);

            var spentRaw = await userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == user.Id && e.CreatedAt >= from && e.CreatedAt <= now)
                .GroupBy(e => e.Category)
                .Select(g => new { g.Key, Amount = g.Sum(e => e.Amount) })
                .ToListAsync();

            var spentByCategory = spentRaw
                .GroupBy(c => (c.Key ?? "Other").Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

            return budgets
                .Select(b => new BudgetStatusDto
                {
                    Category = b.Category,
                    MonthlyLimit = b.MonthlyLimit,
                    Spent = spentByCategory.TryGetValue(b.Category.Trim().ToLowerInvariant(), out var spent) ? spent : 0m
                })
                .ToList();
        }
    }
}
