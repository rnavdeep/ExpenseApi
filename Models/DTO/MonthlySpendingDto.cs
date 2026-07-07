namespace Expense.API.Models.DTO
{
    /// <summary>
    /// One bar in the dashboard's monthly-spending chart: SUM(Amount) for a single month.
    /// </summary>
    public class MonthlySpendingDto
    {
        public int Year { get; set; }
        public int Month { get; set; }

        /// <summary>Short month label for the chart axis, e.g. "Jun".</summary>
        public string Label { get; set; }

        public decimal Amount { get; set; }
    }
}
