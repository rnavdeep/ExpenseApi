USE ExpenseAnalyserDb;
GO

-- Dashboard: spending category on Expense (nullable; null = "Other").
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Category'
               AND Object_ID = Object_ID(N'dbo.Expenses'))
BEGIN
    ALTER TABLE dbo.Expenses ADD Category NVARCHAR(64) NULL;
END
GO
