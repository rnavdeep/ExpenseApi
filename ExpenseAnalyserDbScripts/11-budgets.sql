USE ExpenseAnalyserDb;
GO

-- Budgets: per-user, per-category monthly spending limits.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Budgets' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[Budgets](
        [Id] [uniqueidentifier] NOT NULL,
        [UserId] [uniqueidentifier] NOT NULL,
        [Category] [nvarchar](64) NOT NULL,
        [MonthlyLimit] [decimal](18, 2) NOT NULL,
        [UpdatedAt] [datetime2](7) NOT NULL,
     CONSTRAINT [PK_Budgets] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Budgets_UserId_Category] ON [dbo].[Budgets]
    (
        [UserId] ASC,
        [Category] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    ALTER TABLE [dbo].[Budgets] WITH CHECK ADD CONSTRAINT [FK_Budgets_Users_UserId] FOREIGN KEY([UserId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[Budgets] CHECK CONSTRAINT [FK_Budgets_Users_UserId]
END
GO
