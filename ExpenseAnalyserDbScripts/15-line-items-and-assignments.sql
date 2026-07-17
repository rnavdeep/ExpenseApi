USE ExpenseAnalyserDb;
GO

-- LineItems: normalized, per-row line items extracted from a DocumentJobResult's scanned
-- receipt, so individual rows can be assigned to users (denormalized ExpenseId lets a
-- multi-document expense's items be queried/aggregated without joining through DocumentJobResult).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'LineItems' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[LineItems](
        [Id] [uniqueidentifier] NOT NULL,
        [DocumentJobResultId] [uniqueidentifier] NOT NULL,
        [ExpenseId] [uniqueidentifier] NOT NULL,
        [SortOrder] [int] NOT NULL,
        [Description] [nvarchar](max) NULL,
        [Quantity] [nvarchar](max) NULL,
        [Amount] [decimal](18, 2) NULL,
        [RawFieldsJson] [nvarchar](max) NOT NULL,
     CONSTRAINT [PK_LineItems] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_LineItems_DocumentJobResultId] ON [dbo].[LineItems]
    (
        [DocumentJobResultId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_LineItems_ExpenseId] ON [dbo].[LineItems]
    (
        [ExpenseId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    ALTER TABLE [dbo].[LineItems] WITH CHECK ADD CONSTRAINT [FK_LineItems_DocumentJobResults_DocumentJobResultId] FOREIGN KEY([DocumentJobResultId])
    REFERENCES [dbo].[DocumentJobResults] ([Id])
    ON DELETE CASCADE

    ALTER TABLE [dbo].[LineItems] CHECK CONSTRAINT [FK_LineItems_DocumentJobResults_DocumentJobResultId]

    ALTER TABLE [dbo].[LineItems] WITH CHECK ADD CONSTRAINT [FK_LineItems_Expenses_ExpenseId] FOREIGN KEY([ExpenseId])
    REFERENCES [dbo].[Expenses] ([Id])

    ALTER TABLE [dbo].[LineItems] CHECK CONSTRAINT [FK_LineItems_Expenses_ExpenseId]
END
GO

-- LineItemAssignments: which users a line item is split across.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'LineItemAssignments' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[LineItemAssignments](
        [LineItemId] [uniqueidentifier] NOT NULL,
        [UserId] [uniqueidentifier] NOT NULL,
        [AssignedAt] [datetime2](7) NOT NULL,
     CONSTRAINT [PK_LineItemAssignments] PRIMARY KEY CLUSTERED
    (
        [LineItemId] ASC,
        [UserId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_LineItemAssignments_UserId] ON [dbo].[LineItemAssignments]
    (
        [UserId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    ALTER TABLE [dbo].[LineItemAssignments] WITH CHECK ADD CONSTRAINT [FK_LineItemAssignments_LineItems_LineItemId] FOREIGN KEY([LineItemId])
    REFERENCES [dbo].[LineItems] ([Id])
    ON DELETE CASCADE

    ALTER TABLE [dbo].[LineItemAssignments] CHECK CONSTRAINT [FK_LineItemAssignments_LineItems_LineItemId]

    ALTER TABLE [dbo].[LineItemAssignments] WITH CHECK ADD CONSTRAINT [FK_LineItemAssignments_Users_UserId] FOREIGN KEY([UserId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[LineItemAssignments] CHECK CONSTRAINT [FK_LineItemAssignments_Users_UserId]
END
GO
