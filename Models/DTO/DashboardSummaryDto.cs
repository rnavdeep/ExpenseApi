namespace Expense.API.Models.DTO
{
    /// <summary>
    /// KPI strip + category donut for the dashboard homescreen, scoped to the current user and period.
    /// </summary>
    public class DashboardSummaryDto
    {
        /// <summary>SUM(Amount) of expenses I created in the period.</summary>
        public decimal TotalSpent { get; set; }

        /// <summary>COUNT of documents I uploaded in the period.</summary>
        public int ReceiptsScanned { get; set; }

        /// <summary>SUM(UserAmount) of my shares on expenses created by others.</summary>
        public double YouOwe { get; set; }

        /// <summary>SUM(UserAmount) owed to me by others on expenses I created.</summary>
        public double OwedToYou { get; set; }

        /// <summary>TotalSpent change vs the previous comparable period, as a percentage.</summary>
        public double TotalSpentDeltaPct { get; set; }

        /// <summary>Spending grouped by category (null buckets into "Other").</summary>
        public List<CategoryBreakdownDto> Categories { get; set; } = new();
    }

    public class CategoryBreakdownDto
    {
        public string Category { get; set; }
        public decimal Amount { get; set; }
    }
}
