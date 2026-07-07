-- =============================================================================
-- Realistic seed data for the ExpenseAnalyserDb business database.
--
-- Prerequisite: register the primary user through the API FIRST so the Identity
-- account (and password) exist and login works, e.g. via requests.http:
--     POST /api/Auth/Register  { "userName": "alice", ... "roles": ["Writer"] }
-- That call also inserts alice into dbo.Users. This script then looks alice up by
-- username, creates her counterparties (bob, carol) and seeds expenses / receipts /
-- splits so the dashboard endpoints return meaningful, eyeball-able numbers.
--
-- Run order:  01..07,09 (schema)  ->  register alice via API  ->  this script.
-- Idempotent: it clears existing transactional rows (Expenses/Documents/ExpenseUsers)
-- and the bob/carol users, then reseeds. It does NOT delete registered Users.
-- =============================================================================
USE ExpenseAnalyserDb;
GO
SET NOCOUNT ON;

DECLARE @aliceUser NVARCHAR(256) = N'alice';   -- must match the username you registered
DECLARE @aliceId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM dbo.Users WHERE Username = @aliceUser);

IF @aliceId IS NULL
BEGIN
    RAISERROR('User "%s" not found in dbo.Users. Register that user via POST /api/Auth/Register (with a role) before running this seed.', 16, 1, @aliceUser);
    RETURN;
END

-- Reset transactional data so the script is re-runnable (safe on a test database).
DELETE FROM dbo.ExpenseUsers;
DELETE FROM dbo.Documents;
DELETE FROM dbo.Expenses;
DELETE FROM dbo.Users WHERE Username IN (N'bob', N'carol');

-- Counterparties (business rows only; they never log in).
DECLARE @bobId   UNIQUEIDENTIFIER = NEWID();
DECLARE @carolId UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.Users (Id, Username, Email, CreatedAt) VALUES
    (@bobId,   N'bob',   N'bob@test.local',   SYSUTCDATETIME()),
    (@carolId, N'carol', N'carol@test.local', SYSUTCDATETIME());

DECLARE @now DATETIME2 = SYSUTCDATETIME();
DECLARE @thisMonth DATETIME2 = DATEFROMPARTS(YEAR(@now), MONTH(@now), 15);

-- ---- Alice's spending THIS month (drives summary?period=month) ---------------
DECLARE @travel UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.Expenses (Id, Title, Description, Amount, CreatedAt, CreatedById, Category) VALUES
    (@travel, N'Flight',    N'Flight to NYC',  300.00, @thisMonth, @aliceId, N'Travel'),
    (NEWID(), N'Groceries', N'Weekly shop',    120.50, @thisMonth, @aliceId, N'Groceries'),
    (NEWID(), N'Dinner',    N'Team dinner',      45.00, @thisMonth, @aliceId, N'Dining'),
    (NEWID(), N'Misc',      N'Uncategorised',    30.00, @thisMonth, @aliceId, NULL);
-- Expected summary TotalSpent (month) = 495.50; Categories: Travel 300, Groceries 120.50, Dining 45, Other 30.

-- ---- Alice's spending in PRIOR months (drives monthly chart) -----------------
INSERT INTO dbo.Expenses (Id, Title, Description, Amount, CreatedAt, CreatedById, Category) VALUES
    (NEWID(), N'Groceries', N'Last month shop', 80.00, DATEADD(MONTH, -1, @thisMonth), @aliceId, N'Groceries'),
    (NEWID(), N'Hotel',     N'2 months ago',   150.00, DATEADD(MONTH, -2, @thisMonth), @aliceId, N'Travel'),
    (NEWID(), N'Dinner',    N'3 months ago',    60.00, DATEADD(MONTH, -3, @thisMonth), @aliceId, N'Dining');
-- Expected monthly?months=6: current 495.50, -1 80, -2 150, -3 60, others 0.

-- ---- Receipts uploaded by Alice this month (drives ReceiptsScanned) ----------
INSERT INTO dbo.Documents (Id, FileName, FileExtension, S3Url, ETag, VersionId, Size, UploadedAt, UserId, ExpenseId) VALUES
    (NEWID(), N'flight.pdf',  N'.pdf', N'https://example.com/flight.pdf',  N'etag1', N'v1', 2048, @thisMonth, @aliceId, @travel),
    (NEWID(), N'grocery.jpg', N'.jpg', N'https://example.com/grocery.jpg', N'etag2', N'v1', 1024, @thisMonth, @aliceId, @travel);
-- Expected ReceiptsScanned (month) = 2.

-- ---- Splits: Bob & Carol owe Alice on her Travel expense (OwedToYou) ---------
INSERT INTO dbo.ExpenseUsers (ExpenseId, UserId, UserShare, UserAmount) VALUES
    (@travel, @bobId,   0.33, 100.00),
    (@travel, @carolId, 0.33,  50.00);
-- Expected OwedToYou = 150; balances.OwedToYou = [bob 100, carol 50].

-- ---- A Bob-created expense on which Alice owes 45 (YouOwe) --------------------
DECLARE @concert UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.Expenses (Id, Title, Description, Amount, CreatedAt, CreatedById, Category) VALUES
    (@concert, N'Concert', N'Concert tickets', 90.00, @thisMonth, @bobId, N'Entertainment');
INSERT INTO dbo.ExpenseUsers (ExpenseId, UserId, UserShare, UserAmount) VALUES
    (@concert, @aliceId, 0.50, 45.00);
-- Expected YouOwe = 45; balances.YouOwe = [bob 45].

PRINT 'Seed complete for user alice (+ bob, carol).';
GO
