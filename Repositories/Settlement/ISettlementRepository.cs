using Expense.API.Models.DTO;

namespace Expense.API.Repositories.Settlement
{
    public interface ISettlementRepository
    {
        /// <summary>
        /// Record a settlement paid by the logged in user to the given payee.
        /// </summary>
        Task<SettlementDto> CreateAsync(CreateSettlementDto createSettlementDto);

        /// <summary>
        /// Settlements where the logged in user is payer or payee, newest first.
        /// </summary>
        Task<List<SettlementDto>> GetForUserAsync(int pageNumber, int pageSize);
    }
}
