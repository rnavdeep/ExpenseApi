using System;
using Microsoft.EntityFrameworkCore;
using NSWalks.API.Models.Domain;

namespace NSWalks.API.Data
{
	public class UserDocumentsDbContext: DbContext
	{
        public UserDocumentsDbContext(DbContextOptions<UserDocumentsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Defining One-to-Many relationship between User and Document
            modelBuilder.Entity<User>()
                .HasMany(u => u.Documents)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId);
        }
    }
}

