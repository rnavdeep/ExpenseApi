namespace Expense.API.Models.DTO
{
    /// <summary>
    /// Net balance with a single counterparty plus the chronological ledger (expenses + settlements)
    /// that makes it up.
    /// </summary>
    public class BalanceDetailDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }

        /// <summary>Absolute net amount outstanding between the caller and this counterparty.</summary>
        public double NetAmount { get; set; }

        /// <summary>"youOwe" | "owedToYou" | "settled", relative to the caller.</summary>
        public string Direction { get; set; }

        /// <summary>Chronological (oldest first) expense shares and settlements between the two users.</summary>
        public List<BalanceDetailEntryDto> Entries { get; set; } = new();
    }

    public class BalanceDetailEntryDto
    {
        /// <summary>"expense" | "settlement".</summary>
        public string Type { get; set; }

        public Guid Id { get; set; }
        public string Description { get; set; }
        public double Amount { get; set; }

        /// <summary>"youOwe" | "owedToYou", relative to the caller.</summary>
        public string Direction { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
