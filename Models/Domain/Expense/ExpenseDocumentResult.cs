namespace Expense.API.Models.Domain
{
    public class ExpenseDocumentResult
    {
        public string DocumentName { get; set; }
        public List<ExpenseSummaryField> SummaryFields { get; set; }
        public List<LineItem> LineItems { get; set; }

        public ExpenseDocumentResult()
        {
            SummaryFields = new List<ExpenseSummaryField>();
            LineItems = new List<LineItem>();
        }
    }

    public class ExpenseSummaryField
    {
        public string? FieldName { get; set; }
        public string? FieldValue { get; set; }
    }

    public class LineItem
    {
        public string? Description { get; set; }
        public string? Quantity { get; set; }
        public string? Amount { get; set; }
    }

}

