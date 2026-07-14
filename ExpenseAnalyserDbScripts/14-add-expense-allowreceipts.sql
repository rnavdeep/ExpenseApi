USE ExpenseAnalyserDb;
GO

-- Manual amount + receipt-lock toggle: set only at creation, permanently disables
-- receipt upload/scan for an expense when false. Existing rows default to true
-- (receipts allowed) so current behavior is unchanged for them.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'AllowReceipts'
               AND Object_ID = Object_ID(N'dbo.Expenses'))
BEGIN
    ALTER TABLE dbo.Expenses ADD AllowReceipts BIT NOT NULL DEFAULT 1;
END
GO
