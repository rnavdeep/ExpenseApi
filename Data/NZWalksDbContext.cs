﻿using System;
using Microsoft.EntityFrameworkCore;
using NSWalks.API.Models.Domain;

namespace NSWalks.API.Data
{
	public class NZWalksDbContext: DbContext
	{
		public NZWalksDbContext(DbContextOptions options): base(options)
		{

		}

		public DbSet<Difficulty> Difficulties { get; set; }

		public DbSet<Region> Regions { get; set; }

		public DbSet<Walks> Walks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Difficulty>()
				.HasMany(e => e.Walks)
				.WithOne(e => e.Difficulty)
				.HasForeignKey(e => e.DifficultyId)
				.IsRequired(true);

            modelBuilder.Entity<Region>()
                .HasMany(e => e.Walks)
                .WithOne(e => e.Region)
                .HasForeignKey(e => e.RegionId)
                .IsRequired(true);
        }
    }
}
