using System;
namespace Expense.API.Models.DTO
{
	public class DocumentResultDto
	{
		public DocumentResultDto()
		{
		}
        public Guid Id { get; set; }

        /// <summary>
        /// Store total extracted from documents from summaryFields
        /// </summary>
        public decimal? Total { get; set; }

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

        /// <summary>
        /// Result created at
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// User ID of the result's creator
        /// </summary>
        public Guid CreatedById { get; set; }

        /// <summary>
        /// Expense ID to which this result belongs
        /// </summary>
        public Guid ExpenseId { get; set; }

        /// <summary>
        /// Document ID to which this result belongs
        /// </summary>
        public Guid DocumentId { get; set; }

    }
}

