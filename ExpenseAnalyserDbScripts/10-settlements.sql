USE ExpenseAnalyserDb;
GO

-- Settlements: manual payments between two users, net against expense-share balances.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Settlements' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[Settlements](
        [Id] [uniqueidentifier] NOT NULL,
        [PayerId] [uniqueidentifier] NOT NULL,
        [PayeeId] [uniqueidentifier] NOT NULL,
        [Amount] [decimal](18, 2) NOT NULL,
        [Note] [nvarchar](max) NULL,
        [CreatedAt] [datetime2](7) NOT NULL,
     CONSTRAINT [PK_Settlements] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_Settlements_PayerId] ON [dbo].[Settlements]
    (
        [PayerId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_Settlements_PayeeId] ON [dbo].[Settlements]
    (
        [PayeeId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    ALTER TABLE [dbo].[Settlements] WITH CHECK ADD CONSTRAINT [FK_Settlements_Users_PayerId] FOREIGN KEY([PayerId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[Settlements] CHECK CONSTRAINT [FK_Settlements_Users_PayerId]

    ALTER TABLE [dbo].[Settlements] WITH CHECK ADD CONSTRAINT [FK_Settlements_Users_PayeeId] FOREIGN KEY([PayeeId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[Settlements] CHECK CONSTRAINT [FK_Settlements_Users_PayeeId]
END
GO
