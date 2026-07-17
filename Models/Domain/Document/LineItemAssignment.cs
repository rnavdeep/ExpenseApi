using System;

namespace Expense.API.Models.Domain
{
    public class LineItemAssignment
    {
        public LineItemAssignment()
        {
        }

        /// <summary>
        /// Foreign key for the associated LineItem. Part of the composite primary key.
        /// </summary>
        public Guid LineItemId { get; set; }

        /// <summary>
        /// Navigation property to the associated LineItem.
        /// </summary>
        public LineItem LineItem { get; set; }

        /// <summary>
        /// Foreign key for the associated User. Part of the composite primary key.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Navigation property to the associated User.
        /// </summary>
        public User User { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
