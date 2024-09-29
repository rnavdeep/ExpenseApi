﻿using System;
using Microsoft.EntityFrameworkCore;
using Expense.API.Models.Domain;
using ExpenseModel = Expense.API.Models.Domain.Expense;

namespace Expense.API.Data
{
	public class UserDocumentsDbContext: DbContext
	{
        public UserDocumentsDbContext(DbContextOptions<UserDocumentsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<ExpenseModel> Expenses { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<ExpenseUser> ExpenseUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure the many-to-many relationship between Expense and User
            modelBuilder.Entity<ExpenseUser>()
                .HasKey(eu => new { eu.ExpenseId, eu.UserId });

            modelBuilder.Entity<ExpenseUser>()
                .HasOne(eu => eu.Expense)
                .WithMany(e => e.ExpenseUsers)
                .HasForeignKey(eu => eu.ExpenseId);

            modelBuilder.Entity<ExpenseUser>()
                .HasOne(eu => eu.User)
                .WithMany(u => u.ExpenseUsers)
                .HasForeignKey(eu => eu.UserId);
            modelBuilder.Entity<ExpenseModel>()
                .HasOne(e => e.CreatedBy)
                .WithMany() 
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);  
        }
    }
}

