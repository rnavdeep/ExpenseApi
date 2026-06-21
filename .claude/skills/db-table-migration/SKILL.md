---
name: db-table-migration
description: Add a new table or modify an existing table in this ASP.NET Core (.NET 7) Expense API using EF Core code-first. Covers creating/altering domain entities, DbSet registration, relationship config in OnModelCreating, and generating/applying migrations against the correct DbContext. Use when adding columns, tables, indexes, foreign keys, or relationships.
---

# Database Table & Migration changes

This project is **EF Core code-first**. The database schema is defined by C# domain entities + `OnModelCreating` config, and changed by generating migrations. There are **two separate DbContexts/databases** — always target the right one.

Read `database-schema.md` (in this skill folder) for the current relational structure before changing anything.

## The two DbContexts (pick correctly)

| Context | File | Connection string | Holds |
|---|---|---|---|
| `UserDocumentsDbContext` | `Data/UserDocumentsDbContext.cs` | `ExpenseConnectionString` | **App data** — Users, Expenses, Documents, ExpenseUsers, DocumentJobResults, Notification, FriendRequests |
| `ExpenseAuthDbContext` | `Data/ExpenseAuthDbContext.cs` | `ExpenseAuthConnectionString` | ASP.NET **Identity** (auth users, roles, seeded Reader/Writer/Admin roles) |

Almost all feature/table work goes in **`UserDocumentsDbContext`**. Only touch `ExpenseAuthDbContext` for auth/identity/role changes.

Both are registered in `Configurations/DatabaseConfig.cs`.

## Conventions (match the existing entities)

- Entity classes live in `Models/Domain/<Feature>/<Name>.cs`, namespace `Expense.API.Models.Domain`.
- Primary key: `public Guid Id { get; set; }`.
- Timestamps: `public DateTime CreatedAt { get; set; } = DateTime.UtcNow;` (nullable `DateTime?` for optional ones like `AcceptedAt`, `ReadAt`).
- Status/flag fields use `byte` with an XML-doc comment listing the meanings (e.g. `0 - Unread, 1 - Read`).
- Money: `decimal` (e.g. `Amount`, `Total`); fractional shares use `double`.
- Foreign keys: a `Guid <Entity>Id` scalar **plus** a navigation property `public <Entity> <Entity> { get; set; }`. Collections use `ICollection<T>`.
- Store complex/variable data as JSON in `string?` columns (see `DocumentJobResult.ResultLineItems`, `SummaryFields`, `ColumnNames`).
- Document each property with `/// <summary>` comments — every entity in this repo does.

## Add a NEW table

1. **Create the entity** in `Models/Domain/<Feature>/<Name>.cs` following the conventions above.
2. **Add a `DbSet`** to `Data/UserDocumentsDbContext.cs`:
   ```csharp
   public DbSet<Foo> Foos { get; set; }
   ```
3. **Configure relationships** in `OnModelCreating` if the table has FKs, composite keys, unique indexes, or delete behavior. Examples already in the context:
   - Composite key: `modelBuilder.Entity<ExpenseUser>().HasKey(eu => new { eu.ExpenseId, eu.UserId });`
   - One-to-many + FK + restrict: the `Expense → CreatedBy` config.
   - One-to-one + unique index: the `FriendRequest → Notification` config.
   - Cascade delete: the `Notification → User` config.
   EF will infer simple FKs from the `Guid XId` + navigation convention; add explicit config only when you need composite keys, delete behavior, required/optional, or indexes.
4. **Generate & apply** the migration (see below).

## Modify an EXISTING table

1. Edit the entity in `Models/Domain/...` — add/rename/remove properties, or change types.
2. Update `OnModelCreating` if the change involves keys, indexes, FKs, or delete behavior.
3. If a **DTO** exposes the changed shape, update `Models/DTO/` and the map in `Mappings/AutomapperProfiles.cs`.
4. Generate & apply a migration.

> Renames: EF treats a renamed property as drop+add by default (data loss). If you must preserve data, hand-edit the generated migration to use `migrationBuilder.RenameColumn(...)`.

## Generate & apply a migration

Requires the EF CLI: `dotnet tool install --global dotnet-ef`.

Because there are two contexts, **always pass `--context`**. Keep app-data migrations in their own output folder:

```bash
# App data (default for table work)
dotnet ef migrations add <DescriptiveName> \
  --context UserDocumentsDbContext \
  -o Migrations/UserDocumentsDb

dotnet ef database update --context UserDocumentsDbContext
```

For auth/identity changes use `--context ExpenseAuthDbContext` (its migrations live directly in `Migrations/`).

Inspect the generated `Up`/`Down` methods before applying — confirm no unintended column drops. To roll back the last migration before it's applied:

```bash
dotnet ef migrations remove --context UserDocumentsDbContext
```

## Verify

```bash
dotnet build ExpenseApi.sln
dotnet ef migrations list --context UserDocumentsDbContext   # confirm it's recorded
```

After `database update`, the new/changed table exists in SQL Server. With Docker, the DB runs in the `db` service (see `DOCKER_SETUP.md` / `docker-compose.yml`).

## Notes

- Raw SQL bootstrap scripts exist in `ExpenseAnalyserDbScripts/` (`01-create_db.sql` … `07-notification.sql`) for reference / fresh setup, but the **code-first migrations are the source of truth** — change entities, not those scripts, for ongoing work.
- Entities are reachable from a controller only through a repository (`Repositories/<Feature>/`). After adding a table you'll usually pair it with the `add-api-endpoint` skill to expose it.
