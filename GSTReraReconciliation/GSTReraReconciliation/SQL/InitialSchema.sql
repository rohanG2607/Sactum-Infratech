-- ================================================================
-- GST vs RERA Bank Statement Reconciliation
-- SQL Server — Complete Database Schema
-- Sactum Infratech
-- ================================================================

-- ================================================================
-- Create Database (uncomment for manual execution)
-- ================================================================
-- CREATE DATABASE GSTReraReconciliationDb;
-- GO
-- USE GSTReraReconciliationDb;
-- GO

-- ================================================================
-- Table: UploadSessions
-- Tracks each file upload session.
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UploadSessions]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[UploadSessions] (
        [Id]          INT       IDENTITY(1,1) NOT NULL,
        [UploadDate]  DATETIME  NOT NULL CONSTRAINT [DF_UploadSessions_UploadDate] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_UploadSessions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Index: Frequently filtered/sorted by UploadDate
CREATE NONCLUSTERED INDEX [IX_UploadSessions_UploadDate]
    ON [dbo].[UploadSessions]([UploadDate] DESC);
GO


-- ================================================================
-- Table: RERARecords
-- Rows parsed from a RERA bank statement Excel file.
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RERARecords]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[RERARecords] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [SessionId]   INT            NOT NULL,
        [Name]        NVARCHAR(500)  NOT NULL,
        [Amount]      DECIMAL(18,2)  NOT NULL,

        CONSTRAINT [PK_RERARecords] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RERARecords_UploadSessions] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[UploadSessions]([Id])
            ON DELETE CASCADE
    );
END
GO

-- Index: FK lookup
CREATE NONCLUSTERED INDEX [IX_RERARecords_SessionId]
    ON [dbo].[RERARecords]([SessionId]);
GO

-- Index: Name-based matching & search
CREATE NONCLUSTERED INDEX [IX_RERARecords_Name]
    ON [dbo].[RERARecords]([Name]);
GO

-- Index: Amount-based matching
CREATE NONCLUSTERED INDEX [IX_RERARecords_Amount]
    ON [dbo].[RERARecords]([Amount]);
GO


-- ================================================================
-- Table: GSTRecords
-- Rows parsed from a GST return/register Excel file.
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GSTRecords]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[GSTRecords] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [SessionId]   INT            NOT NULL,
        [Name]        NVARCHAR(500)  NOT NULL,
        [GSTAmount]   DECIMAL(18,2)  NOT NULL,

        CONSTRAINT [PK_GSTRecords] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_GSTRecords_UploadSessions] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[UploadSessions]([Id])
            ON DELETE CASCADE
    );
END
GO

-- Index: FK lookup
CREATE NONCLUSTERED INDEX [IX_GSTRecords_SessionId]
    ON [dbo].[GSTRecords]([SessionId]);
GO

-- Index: Name-based matching & search
CREATE NONCLUSTERED INDEX [IX_GSTRecords_Name]
    ON [dbo].[GSTRecords]([Name]);
GO

-- Index: Amount-based matching
CREATE NONCLUSTERED INDEX [IX_GSTRecords_GSTAmount]
    ON [dbo].[GSTRecords]([GSTAmount]);
GO


-- ================================================================
-- Table: ComparisonResults
-- Stores the result of each reconciliation comparison row.
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ComparisonResults]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ComparisonResults] (
        [Id]           INT            IDENTITY(1,1) NOT NULL,
        [SessionId]    INT            NOT NULL,
        [RERAName]     NVARCHAR(500)  NULL,
        [GSTName]      NVARCHAR(500)  NULL,
        [ExpectedGST]  DECIMAL(18,2)  NOT NULL CONSTRAINT [DF_ComparisonResults_ExpectedGST] DEFAULT (0),
        [ActualGST]    DECIMAL(18,2)  NOT NULL CONSTRAINT [DF_ComparisonResults_ActualGST] DEFAULT (0),
        [Status]       NVARCHAR(20)   NOT NULL,

        CONSTRAINT [PK_ComparisonResults] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ComparisonResults_UploadSessions] FOREIGN KEY ([SessionId])
            REFERENCES [dbo].[UploadSessions]([Id])
            ON DELETE CASCADE,

        -- Enforce valid status values at the database level
        CONSTRAINT [CK_ComparisonResults_Status] CHECK (
            [Status] IN ('MATCHED', 'RERA_NOT_GST', 'GST_NOT_RERA', 'GST_MISMATCH', 'POSSIBLE_MATCH')
        )
    );
END
GO

-- Index: FK lookup
CREATE NONCLUSTERED INDEX [IX_ComparisonResults_SessionId]
    ON [dbo].[ComparisonResults]([SessionId]);
GO

-- Index: Status-based filtering (dashboard & reports)
CREATE NONCLUSTERED INDEX [IX_ComparisonResults_Status]
    ON [dbo].[ComparisonResults]([Status])
    INCLUDE ([SessionId], [RERAName], [GSTName], [ExpectedGST], [ActualGST]);
GO

-- Index: Name-based search
CREATE NONCLUSTERED INDEX [IX_ComparisonResults_RERAName]
    ON [dbo].[ComparisonResults]([RERAName]);
GO

CREATE NONCLUSTERED INDEX [IX_ComparisonResults_GSTName]
    ON [dbo].[ComparisonResults]([GSTName]);
GO


-- ================================================================
-- Summary View: Quick dashboard stats per session
-- ================================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_ReconciliationSummary')
    DROP VIEW [dbo].[vw_ReconciliationSummary];
GO

CREATE VIEW [dbo].[vw_ReconciliationSummary]
AS
SELECT
    s.[Id]              AS SessionId,
    s.[UploadDate],
    COUNT(cr.[Id])      AS TotalResults,
    SUM(CASE WHEN cr.[Status] = 'MATCHED'        THEN 1 ELSE 0 END) AS MatchedCount,
    SUM(CASE WHEN cr.[Status] = 'RERA_NOT_GST'    THEN 1 ELSE 0 END) AS ReraNotGstCount,
    SUM(CASE WHEN cr.[Status] = 'GST_NOT_RERA'    THEN 1 ELSE 0 END) AS GstNotReraCount,
    SUM(CASE WHEN cr.[Status] = 'GST_MISMATCH'    THEN 1 ELSE 0 END) AS GstMismatchCount,
    SUM(CASE WHEN cr.[Status] = 'POSSIBLE_MATCH'  THEN 1 ELSE 0 END) AS PossibleMatchCount,
    SUM(ABS(cr.[ExpectedGST] - cr.[ActualGST]))   AS TotalDifference
FROM [dbo].[UploadSessions] s
LEFT JOIN [dbo].[ComparisonResults] cr ON s.[Id] = cr.[SessionId]
GROUP BY s.[Id], s.[UploadDate];
GO


PRINT '=== GST RERA Reconciliation schema created successfully ===';
GO
