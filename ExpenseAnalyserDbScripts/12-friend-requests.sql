USE ExpenseAnalyserDb;
GO

-- FriendRequests: pending/accepted friend connections between two users, linked to a Notification.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'FriendRequests' AND Schema_Id = Schema_Id(N'dbo'))
BEGIN
    CREATE TABLE [dbo].[FriendRequests](
        [Id] [uniqueidentifier] NOT NULL,
        [SentByUserId] [uniqueidentifier] NOT NULL,
        [SentToUserId] [uniqueidentifier] NOT NULL,
        [NotificationId] [uniqueidentifier] NOT NULL,
        [CreatedAt] [datetime2](7) NOT NULL,
        [IsAccepted] [tinyint] NOT NULL,
        [AcceptedAt] [datetime2](7) NULL,
     CONSTRAINT [PK_FriendRequests] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_FriendRequests_NotificationId] ON [dbo].[FriendRequests]
    (
        [NotificationId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_FriendRequests_SentByUserId] ON [dbo].[FriendRequests]
    (
        [SentByUserId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    CREATE NONCLUSTERED INDEX [IX_FriendRequests_SentToUserId] ON [dbo].[FriendRequests]
    (
        [SentToUserId] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

    -- NOTE: source EF migration specifies ON DELETE CASCADE for all three FKs below, but SQL Server
    -- rejects that ("may cause cycles or multiple cascade paths") since SentByUserId and SentToUserId
    -- both cascade from Users into this same table. NO ACTION here matches what's actually deployed
    -- locally (verified against sql_server_demo) and is required for this script to succeed.
    ALTER TABLE [dbo].[FriendRequests] WITH CHECK ADD CONSTRAINT [FK_FriendRequests_Notification_NotificationId] FOREIGN KEY([NotificationId])
    REFERENCES [dbo].[Notification] ([Id])

    ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Notification_NotificationId]

    ALTER TABLE [dbo].[FriendRequests] WITH CHECK ADD CONSTRAINT [FK_FriendRequests_Users_SentByUserId] FOREIGN KEY([SentByUserId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Users_SentByUserId]

    ALTER TABLE [dbo].[FriendRequests] WITH CHECK ADD CONSTRAINT [FK_FriendRequests_Users_SentToUserId] FOREIGN KEY([SentToUserId])
    REFERENCES [dbo].[Users] ([Id])

    ALTER TABLE [dbo].[FriendRequests] CHECK CONSTRAINT [FK_FriendRequests_Users_SentToUserId]
END
GO
