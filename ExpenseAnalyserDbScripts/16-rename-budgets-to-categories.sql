USE ExpenseAnalyserDb;
GO

-- Rename Budgets -> Categories: a "budget" row is really a category, which can optionally
-- carry a monthly spending limit. This is a rename, not a drop+recreate, so existing rows,
-- ids, and the UserId/Name uniqueness constraint all survive untouched.
IF EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Budgets' AND Schema_Id = Schema_Id(N'dbo'))
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Categories' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    EXEC sp_rename N'dbo.Budgets', N'Categories', N'OBJECT';
    EXEC sp_rename N'dbo.Categories.Category', N'Name', N'COLUMN';
    EXEC sp_rename N'PK_Budgets', N'PK_Categories', N'OBJECT';
    EXEC sp_rename N'Categories.IX_Budgets_UserId_Category', N'IX_Categories_UserId_Name', N'INDEX';
    EXEC sp_rename N'FK_Budgets_Users_UserId', N'FK_Categories_Users_UserId', N'OBJECT';
END
GO
