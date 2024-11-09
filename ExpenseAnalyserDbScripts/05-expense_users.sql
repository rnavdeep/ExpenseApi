USE ExpenseAnalyser
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ExpenseUsers](
	[ExpenseId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_ExpenseUsers] PRIMARY KEY CLUSTERED 
(
	[ExpenseId] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ExpenseUsers_UserId] ON [dbo].[ExpenseUsers]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ExpenseUsers]  WITH CHECK ADD  CONSTRAINT [FK_ExpenseUsers_Expenses_ExpenseId] FOREIGN KEY([ExpenseId])
REFERENCES [dbo].[Expenses] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ExpenseUsers] CHECK CONSTRAINT [FK_ExpenseUsers_Expenses_ExpenseId]
GO
ALTER TABLE [dbo].[ExpenseUsers]  WITH CHECK ADD  CONSTRAINT [FK_ExpenseUsers_Users_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ExpenseUsers] CHECK CONSTRAINT [FK_ExpenseUsers_Users_UserId]
GO