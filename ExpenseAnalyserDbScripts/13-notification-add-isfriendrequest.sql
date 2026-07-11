USE ExpenseAnalyserDb;
GO

-- Notification: flags whether a notification is a friend-request notification (0/1, nullable).
-- Present in the Notification C# model/migration since FriendRequests was introduced, but missed
-- when 07-notification.sql was originally scripted.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsFriendRequest'
               AND Object_ID = Object_ID(N'dbo.Notification'))
BEGIN
    ALTER TABLE dbo.Notification ADD IsFriendRequest TINYINT NULL;
END
GO
