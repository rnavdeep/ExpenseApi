namespace Expense.API.Models.Domain
{
    public class ExpenseDocumentResult
    {
        public string DocumentName { get; set; }
        public List<ExpenseSummaryField> SummaryFields { get; set; }
        public List<TextractLineItemFields> LineItems { get; set; }

        public ExpenseDocumentResult()
        {
            SummaryFields = new List<ExpenseSummaryField>();
            LineItems = new List<TextractLineItemFields>();
        }
    }

    public class ExpenseSummaryField
    {
        public string? FieldName { get; set; }
        public string? FieldValue { get; set; }
    }

    public class TextractLineItemFields
    {
        public string? Description { get; set; }
        public string? Quantity { get; set; }
        public string? Amount { get; set; }
    }

}

