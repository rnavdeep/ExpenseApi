USE ExpenseAnalyser
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentJobResults](
	[Id] [uniqueidentifier] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[ResultLineItems] [nvarchar](max) NULL,
	[ColumnNames] [nvarchar](max) NULL,
	[CreatedById] [uniqueidentifier] NOT NULL,
	[ExpenseId] [uniqueidentifier] NOT NULL,
	[DocumentId] [uniqueidentifier] NOT NULL,
	[SummaryFields] [nvarchar](max) NULL,
	[ResultCreatedAt] [datetime2](7) NULL,
	[JobId] [nvarchar](max) NOT NULL,
	[Status] [tinyint] NOT NULL,
 CONSTRAINT [PK_DocumentJobResults] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
ALTER TABLE [dbo].[DocumentJobResults] ADD  CONSTRAINT [DEFAULT_DocumentResult_Total]  DEFAULT ((0.0)) FOR [Total]
GO
ALTER TABLE [dbo].[DocumentJobResults] ADD  CONSTRAINT [DEFAULT_DocumentJobResults_Status]  DEFAULT ((0)) FOR [Status]
GO
ALTER TABLE [dbo].[DocumentJobResults]  WITH CHECK ADD  CONSTRAINT [FK_DocumentResult_Documents_DocumentId] FOREIGN KEY([DocumentId])
REFERENCES [dbo].[Documents] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DocumentJobResults] CHECK CONSTRAINT [FK_DocumentResult_Documents_DocumentId]
GO
ALTER TABLE [dbo].[DocumentJobResults]  WITH CHECK ADD  CONSTRAINT [FK_DocumentResult_Expenses_ExpenseId] FOREIGN KEY([ExpenseId])
REFERENCES [dbo].[Expenses] ([Id])
GO
ALTER TABLE [dbo].[DocumentJobResults] CHECK CONSTRAINT [FK_DocumentResult_Expenses_ExpenseId]
GO
ALTER TABLE [dbo].[DocumentJobResults]  WITH CHECK ADD  CONSTRAINT [FK_DocumentResult_Users_CreatedById] FOREIGN KEY([CreatedById])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[DocumentJobResults] CHECK CONSTRAINT [FK_DocumentResult_Users_CreatedById]
GO
