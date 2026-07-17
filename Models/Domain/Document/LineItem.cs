using System;
using System.Collections.Generic;

namespace Expense.API.Models.Domain
{
    public class LineItem
    {
        public LineItem()
        {
        }

        public Guid Id { get; set; }

        /// <summary>
        /// Document job result this line item was extracted from.
        /// </summary>
        public Guid DocumentJobResultId { get; set; }  // Foreign key for DocumentJobResult
        public DocumentJobResult DocumentJobResult { get; set; }  // Navigation property

        /// <summary>
        /// Denormalized so a multi-document expense's items can be queried/aggregated in one
        /// pass without joining through DocumentJobResult.
        /// </summary>
        public Guid ExpenseId { get; set; }  // Foreign key for Expense
        public Expense Expense { get; set; }  // Navigation property

        /// <summary>
        /// Position of this item within its document's extracted line item list.
        /// </summary>
        public int SortOrder { get; set; }

        public string? Description { get; set; }

        public string? Quantity { get; set; }

        public decimal? Amount { get; set; }

        /// <summary>
        /// Full raw per-vendor Textract field dump for this row.
        /// </summary>
        public string RawFieldsJson { get; set; }

        /// <summary>
        /// Users assigned to this line item.
        /// </summary>
        public ICollection<LineItemAssignment> Assignments { get; set; }
    }
}
