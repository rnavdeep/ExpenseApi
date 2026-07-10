using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Expense.API.Data;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Notifications;
using SettlementModel = Expense.API.Models.Domain.Settlement;

namespace Expense.API.Repositories.Settlement
{
    public class SettlementRepository : ISettlementRepository
    {
        private readonly UserDocumentsDbContext userDocumentsDbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IMapper mapper;
        private readonly IServiceProvider serviceProvider;
        private readonly IHubContext<TextractNotificationHub> textractNotification;

        public SettlementRepository(UserDocumentsDbContext userDocumentsDbContext, IHttpContextAccessor httpContextAccessor, IMapper mapper,
            IServiceProvider serviceProvider, IHubContext<TextractNotificationHub> textractNotification)
        {
            this.userDocumentsDbContext = userDocumentsDbContext;
            this.httpContextAccessor = httpContextAccessor;
            this.mapper = mapper;
            this.serviceProvider = serviceProvider;
            this.textractNotification = textractNotification;
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

        public async Task<SettlementDto> CreateAsync(CreateSettlementDto createSettlementDto)
        {
            if (createSettlementDto.Amount <= 0)
            {
                throw new Exception("Amount must be greater than zero.");
            }

            var payer = await GetCurrentUserAsync();

            var payee = await userDocumentsDbContext.Users
                            .FirstOrDefaultAsync(u => u.Id == createSettlementDto.PayeeUserId);

            if (payee == null)
            {
                throw new Exception("Payee not found.");
            }

            if (payee.Id == payer.Id)
            {
                throw new Exception("Payee can not be the same as the current user.");
            }

            var settlement = new SettlementModel
            {
                Id = Guid.NewGuid(),
                PayerId = payer.Id,
                Payer = payer,
                PayeeId = payee.Id,
                Payee = payee,
                Amount = createSettlementDto.Amount,
                Note = createSettlementDto.Note,
                CreatedAt = DateTime.UtcNow
            };

            await userDocumentsDbContext.Settlements.AddAsync(settlement);
            await userDocumentsDbContext.SaveChangesAsync();

            await NotifyPayeeAsync(payer, payee, createSettlementDto.Amount);

            return mapper.Map<SettlementDto>(settlement);
        }

        /// <summary>
        /// Notify the payee that a settlement was recorded. A notification failure must not fail the settlement.
        /// </summary>
        private async Task NotifyPayeeAsync(User payer, User payee, decimal amount)
        {
            try
            {
                var message = $"{payer.Username} settled ${amount} with you";

                using (var scope = serviceProvider.CreateScope())
                {
                    var textractNotificationDb = scope.ServiceProvider.GetRequiredService<ITextractNotification>();
                    await textractNotificationDb.CreateNotifcation(payee.Id, message, "Settlement received", 0);
                }

                await textractNotification.Clients.User(payee.Username).SendAsync("TextractNotification", message);
            }
            catch
            {
                // Swallow: a notification failure must not fail the settlement.
            }
        }

        public async Task<List<SettlementDto>> GetForUserAsync(int pageNumber, int pageSize)
        {
            var user = await GetCurrentUserAsync();

            var settlements = await userDocumentsDbContext.Settlements
                .Where(s => s.PayerId == user.Id || s.PayeeId == user.Id)
                .Include(s => s.Payer)
                .Include(s => s.Payee)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return mapper.Map<List<SettlementDto>>(settlements);
        }
    }
}
