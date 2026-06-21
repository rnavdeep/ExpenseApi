USE ExpenseAnalyserDb
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FriendRequests](
	[Id] [uniqueidentifier] NOT NULL,
	[SentByUserId] [uniqueidentifier] NOT NULL,
	[SentToUserId] [uniqueidentifier] NOT NULL,
	[NotificationId] [uniqueidentifier] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[IsAccepted] [tinyint] NOT NULL CONSTRAINT [DF_FriendRequests_IsAccepted] DEFAULT ((0)),
	[AcceptedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_FriendRequests] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_FriendRequests_NotificationId] ON [dbo].[FriendRequests]
(
	[NotificationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_FriendRequests_SentByUserId] ON [dbo].[FriendRequests]
(
	[SentByUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_FriendRequests_SentToUserId] ON [dbo].[FriendRequests]
(
	[SentToUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[FriendRequests]  WITH CHECK ADD  CONSTRAINT [FK_FriendRequests_Notification_NotificationId] FOREIGN KEY([NotificationId])
REFERENCES [dbo].[Notification] ([Id])
GO
ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Notification_NotificationId]
GO
ALTER TABLE [dbo].[FriendRequests]  WITH CHECK ADD  CONSTRAINT [FK_FriendRequests_Users_SentByUserId] FOREIGN KEY([SentByUserId])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Users_SentByUserId]
GO
ALTER TABLE [dbo].[FriendRequests]  WITH CHECK ADD  CONSTRAINT [FK_FriendRequests_Users_SentToUserId] FOREIGN KEY([SentToUserId])
REFERENCES [dbo].[Users] ([Id])
GO
ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Users_SentToUserId]
GO
