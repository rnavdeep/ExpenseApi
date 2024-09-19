using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NSWalks.API.Models.Domain
{
	public class User
	{
		public User()
		{
		}
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property - A user can have many documents
        public ICollection<Document> Documents { get; set; } = new List<Document>();

    }
}

