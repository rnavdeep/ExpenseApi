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
        public String JobId { get; set; }

        /// <summary>
        /// 0 - Pending
        /// 1 - Success
        /// 2 - Failed
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Job created at
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Result created at
        /// </summary>
        public DateTime? ResultCreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Document Result Id
        /// </summary>
        public Guid? DocumentResultId { get; set; }  // Foreign key for User
        public DocumentResult? DocumentResult { get; set; }     // Navigation property

    }
}

