using System;
namespace Expense.API.Models.Domain
{
	public class DocumentResult
	{
		public DocumentResult()
		{
		}

        public DocumentResult( decimal? total, string? resultLineItems, string? columnNames, string? summaryFields, DateTime createdAt, Guid createdById, Guid expenseId, Guid documentId)
        {
            Total = total;
            ResultLineItems = resultLineItems;
            ColumnNames = columnNames;
            SummaryFields = summaryFields;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ExpenseId = expenseId;
            DocumentId = documentId;
        }

        public Guid Id { get; set; }

        /// <summary>
        /// Store total extracted from documents from summaryfields
        /// </summary>
        public decimal? Total { get; set; }

        /// <summary>
        /// Json Represting line items
        /// </summary>
        public string? ResultLineItems { get; set; }

        /// <summary>
        /// Json represting all the columns of line item expense fields
        /// </summary>
        public string? ColumnNames { get; set; }

        /// <summary>
        /// Json represting summaryFields from expenseDocuments
        /// </summary>
        public string? SummaryFields { get; set; }

        /// <summary>
        /// Result created at
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User the result is processed by.
        /// </summary>
        public Guid CreatedById { get; set; }  // Foreign key for User
        public User CreatedBy { get; set; }     // Navigation property

        //Navgational Properties
        /// <summary>
        /// Expense 
        /// </summary>
        public Guid ExpenseId { get; set; }  // Foreign key for Expense
        public Expense Expense { get; set; }     // Navigation property

        //Navgational Properties
        /// <summary>
        /// Document 
        /// </summary>
        public Guid DocumentId { get; set; }  // Foreign key for Document
        public Document Document { get; set; }     // Navigation property
    }
}

