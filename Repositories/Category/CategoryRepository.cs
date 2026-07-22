using System.Security.Claims;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using CategoryModel = Expense.API.Models.Domain.Category;

namespace Expense.API.Repositories.Category
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMapper mapper;

        public CategoryRepository(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor, IMapper mapper)
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

        public async Task<CategoryDto> UpsertAsync(UpsertCategoryDto upsertCategoryDto)
        {
            if (upsertCategoryDto.MonthlyLimit <= 0)
            {
                throw new Exception("Monthly limit must be greater than zero.");
            }

            var name = upsertCategoryDto.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Category can not be blank.");
            }

            var user = await GetCurrentUserAsync();

            var category = await userDocumentsDbContext.Categories
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Name.ToLower() == name.ToLower());

            if (category == null)
            {
                category = new CategoryModel
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Name = name,
                    MonthlyLimit = upsertCategoryDto.MonthlyLimit,
                    UpdatedAt = DateTime.UtcNow
                };
                await userDocumentsDbContext.Categories.AddAsync(category);
            }
            else
            {
                category.MonthlyLimit = upsertCategoryDto.MonthlyLimit;
                category.UpdatedAt = DateTime.UtcNow;
            }

            await userDocumentsDbContext.SaveChangesAsync();

            return mapper.Map<CategoryDto>(category);
        }

        public async Task<List<CategoryStatusDto>> GetStatusAsync(string period)
        {
            var user = await GetCurrentUserAsync();

            var categories = await userDocumentsDbContext.Categories
                .Where(c => c.UserId == user.Id)
                .ToListAsync();

            if (!categories.Any())
            {
                return new List<CategoryStatusDto>();
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

            return categories
                .Select(c => new CategoryStatusDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    MonthlyLimit = c.MonthlyLimit,
                    Spent = spentByCategory.TryGetValue(c.Name.Trim().ToLowerInvariant(), out var spent) ? spent : 0m
                })
                .ToList();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var user = await GetCurrentUserAsync();

            var category = await userDocumentsDbContext.Categories
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

            if (category == null)
            {
                return false;
            }

            userDocumentsDbContext.Categories.Remove(category);
            await userDocumentsDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<CategoryExpenseDto>> GetCategoryExpensesAsync(string name, int take = 5)
        {
            var user = await GetCurrentUserAsync();

            var now = DateTime.UtcNow;
            var from = new DateTime(now.Year, now.Month, 1);
            var normalizedName = (name ?? "Other").Trim().ToLowerInvariant();

            return await userDocumentsDbContext.Expenses
                .Where(e => e.CreatedById == user.Id && e.CreatedAt >= from && e.CreatedAt <= now)
                .Where(e => (e.Category ?? "Other").Trim().ToLower() == normalizedName)
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .Select(e => new CategoryExpenseDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Amount = e.Amount,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();
        }
    }
}
