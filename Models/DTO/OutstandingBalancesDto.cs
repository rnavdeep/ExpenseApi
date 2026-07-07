namespace Expense.API.Models.DTO
{
    /// <summary>
    /// Outstanding balances panel: gross amounts owed per counterparty, split by direction.
    /// </summary>
    public class OutstandingBalancesDto
    {
        /// <summary>People I owe (my shares on expenses they created).</summary>
        public List<BalanceEntryDto> YouOwe { get; set; } = new();

        /// <summary>People who owe me (their shares on expenses I created).</summary>
        public List<BalanceEntryDto> OwedToYou { get; set; } = new();
    }

    public class BalanceEntryDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public double Amount { get; set; }
    }
}
