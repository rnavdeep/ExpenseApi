using System;

namespace Expense.API.Models.Domain
{
    public class DocumentJobResult
    {
        public DocumentJobResult()
        {
        }
        public Guid Id { get; set; }

        /// <summary>
        /// Job Id
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// 0 - Pending, 1 - Success, 2 - Failed
        /// </summary>
        public byte Status { get; set; } = 0;  // Default value for Status

        /// <summary>
        /// Job created at
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Result created at
        /// </summary>
        public DateTime? ResultCreatedAt { get; set; }

        /// <summary>
        /// User the result is processed by.
        /// </summary>
        public Guid CreatedById { get; set; }  // Foreign key for User
        public User CreatedBy { get; set; }     // Navigation property

        /// <summary>
        /// Expense 
        /// </summary>
        public Guid ExpenseId { get; set; }  // Foreign key for Expense
        public Expense Expense { get; set; } // Navigation property

        /// <summary>
        /// Document 
        /// </summary>
        public Guid DocumentId { get; set; }  // Foreign key for Document
        public Document Document { get; set; } // Navigation property

        /// <summary>
        /// Store total extracted from documents from summaryfields
        /// </summary>
        public decimal Total { get; set; } = 0.0m;  // Default value for Total

        /// <summary>
        /// JSON representing line items
        /// </summary>
        public string? ResultLineItems { get; set; }

        /// <summary>
        /// JSON representing all the columns of line item expense fields
        /// </summary>
        public string? ColumnNames { get; set; }

        /// <summary>
        /// JSON representing summaryFields from expenseDocuments
        /// </summary>
        public string? SummaryFields { get; set; }
    }
}
