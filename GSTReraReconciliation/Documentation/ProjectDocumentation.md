# GST vs RERA Bank Statement Reconciliation System

## Project Documentation

---

**Organization:** Sactum Infratech  
**Project:** GST vs RERA Bank Statement Reconciliation  
**Version:** 1.0  
**Framework:** ASP.NET MVC 5 (.NET Framework 4.8)  
**Document Date:** June 2026  

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [System Architecture](#2-system-architecture)
3. [Database Design](#3-database-design)
4. [Module Description](#4-module-description)
5. [Setup Guide](#5-setup-guide)
6. [Installation Steps](#6-installation-steps)
7. [SQL Deployment Steps](#7-sql-deployment-steps)
8. [User Guide](#8-user-guide)
9. [Project Flow Diagram](#9-project-flow-diagram)
10. [Assumptions](#10-assumptions)
11. [Future Enhancements](#11-future-enhancements)

---

## 1. Project Overview

### 1.1 Introduction

The **GST vs RERA Bank Statement Reconciliation System** is a web-based application designed to automate the process of reconciling RERA (Real Estate Regulatory Authority) bank statement entries with GST (Goods and Services Tax) return records.

In the real estate industry, companies must ensure that every financial transaction recorded in RERA bank statements is accurately reflected in their GST filings. Manual reconciliation of these records is time-consuming, error-prone, and resource-intensive. This system eliminates manual effort by automating comparison, discrepancy detection, and report generation.

### 1.2 Problem Statement

Real estate companies face the following challenges:

- **Volume:** Hundreds to thousands of transactions need cross-verification each period.
- **Name Variations:** The same entity may appear with slightly different names across RERA and GST records (e.g., "ABC Builders Pvt Ltd" vs "ABC Builders Private Limited").
- **Amount Mismatches:** Expected GST amounts (calculated at 5% of RERA amounts) may not match actual GST filings due to errors, adjustments, or omissions.
- **Missing Entries:** Transactions may exist in one system but not the other.

### 1.3 Solution

This system provides:

| Feature | Description |
|---------|-------------|
| **Excel Import** | Upload RERA bank statements and GST return Excel files (.xlsx/.xls) |
| **Automated Matching** | Exact name matching + fuzzy matching (FuzzySharp) + amount matching |
| **GST Calculation** | Automatic Expected GST computation (Amount × 5%) |
| **6-Status Classification** | MATCHED, LIKELY_MATCH, POSSIBLE_MATCH, GST_MISMATCH, MISSING_IN_GST, MISSING_IN_RERA |
| **Dashboard** | Real-time summary with 6 status cards, progress bar, and session filter |
| **Reports** | 7-Tab filterable, sortable (by scores), paginated, and searchable report views |
| **Excel Export** | Multi-sheet detailed workbook export with color-coded formatting and summary stats |

### 1.4 Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | ASP.NET MVC 5 | 5.2.9 |
| Runtime | .NET Framework | 4.8 |
| ORM | Entity Framework (Code First) | 6.5.1 |
| Database | SQL Server | 2016+ |
| Excel I/O | EPPlus | 4.5.3.3 |
| Fuzzy Matching | FuzzySharp | 2.0.2 |
| Frontend CSS | Bootstrap | 5.3.3 |
| Icons | Bootstrap Icons | CDN |
| JavaScript | jQuery | 3.7.1 |
| JSON | Newtonsoft.Json | 13.0.3 |

---

## 2. System Architecture

### 2.1 Architecture Pattern

The application follows a **Layered Architecture** pattern with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │   Home   │  │  Upload  │  │Dashboard │  │ Reports  │     │
│  │  View    │  │  View    │  │  View    │  │  View    │     │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘     │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              Shared Layout (_Layout.cshtml)           │  │
│  └───────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    CONTROLLER LAYER                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │  Home    │  │  Upload  │  │Dashboard │  │ Reports  │     │
│  │Controller│  │Controller│  │Controller│  │Controller│     │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘     │
├─────────────────────────────────────────────────────────────┤
│                     SERVICE LAYER                           │
│  ┌─────────────────┐ ┌──────────────────┐ ┌─────────────┐   │
│  │ ExcelImport     │ │  Comparison      │ │   Export    │   │
│  │ Service         │ │  Service         │ │   Service   │   │
│  │                 │ │                  │ │             │   │
│  │ • File Validate │ │ • Name Normalize │ │ • EPPlus    │   │
│  │ • EPPlus Parse  │ │ • Exact Match    │ │ • Multi-Sheet│  │
│  │ • Secure Store  │ │ • Fuzzy Match    │ │ • Color-Code│   │
│  │ • EF Save       │ │ • GST Calculate  │ │ • Currency  │   │
│  └─────────────────┘ └──────────────────┘ └─────────────┘   │
├─────────────────────────────────────────────────────────────┤
│                   REPOSITORY LAYER                          │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              GenericRepository<T>                     │  │
│  │  • GetByIdAsync()  • Find()  • AddRange()             │  │
│  │  • GetAllAsync()   • Add()   • SaveChangesAsync()     │  │
│  └───────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                      DATA LAYER                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │         ReconciliationDbContext (EF6)                 │  │
│  │  • UploadSessions  • GSTRecords                       │  │
│  │  • RERARecords     • ComparisonResults                │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │                  SQL Server                           │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Design Patterns Used

| Pattern | Usage |
|---------|-------|
| **MVC (Model-View-Controller)** | Core application structure — Models define data, Views render UI, Controllers handle HTTP requests |
| **Repository Pattern** | `GenericRepository<T>` abstracts data access, enabling testability and decoupling from EF |
| **Service Layer Pattern** | Business logic encapsulated in `ExcelImportService`, `ComparisonService`, `ExportService` |
| **ViewModel Pattern** | `UploadViewModel`, `DashboardViewModel`, `ReportFilterViewModel` decouple view data from domain models |
| **PRG (Post-Redirect-Get)** | Upload and Compare actions use POST → redirect → GET to prevent duplicate submissions |
| **Code First Migrations** | Database schema managed via EF Code First with Fluent API |

### 2.3 Project Folder Structure

```
GSTReraReconciliation/
├── App_Start/
│   ├── RouteConfig.cs              # MVC routing rules
│   ├── BundleConfig.cs             # CSS/JS bundling
│   └── FilterConfig.cs            # Global filters
│
├── Controllers/
│   ├── HomeController.cs           # Landing page
│   ├── UploadController.cs         # File upload & compare trigger
│   ├── DashboardController.cs      # Dashboard statistics
│   └── ReportsController.cs        # Filtered reports & Excel export
│
├── Models/
│   ├── UploadSession.cs            # Upload session entity
│   ├── RERARecord.cs               # RERA bank statement row
│   ├── GSTRecord.cs                # GST return row
│   └── ComparisonResult.cs         # Reconciliation result + ReconciliationStatus
│
├── ViewModels/
│   ├── UploadViewModel.cs          # Dual-upload form state
│   ├── DashboardViewModel.cs       # Dashboard summary counts
│   └── ReportFilterViewModel.cs    # Report filters, paging, sorting
│
├── Data/
│   ├── ReconciliationDbContext.cs   # EF6 DbContext + Fluent API
│   └── Repositories/
│       ├── IGenericRepository.cs    # Repository interface
│       └── GenericRepository.cs     # Generic EF repository
│
├── Services/
│   ├── IExcelImportService.cs      # Import interface
│   ├── ExcelImportService.cs       # EPPlus-based Excel parser
│   ├── IComparisonService.cs       # Comparison interface
│   ├── ComparisonService.cs        # Matching algorithm
│   ├── IExportService.cs           # Export interface
│   └── ExportService.cs            # EPPlus-based Excel generator
│
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml          # Master layout with navbar
│   │   └── _Error.cshtml           # Error page
│   ├── Home/Index.cshtml           # Welcome page
│   ├── Upload/Index.cshtml         # Dual upload + compare UI
│   ├── Dashboard/Index.cshtml      # Status cards + results table
│   └── Reports/Index.cshtml        # Tabbed reports with paging
│
├── SQL/
│   └── InitialSchema.sql           # Database creation script
│
├── Migrations/
│   └── Configuration.cs            # EF migration configuration
│
├── Content/
│   └── Site.css                    # Custom CSS
│
├── Web.config                      # Application configuration
├── Global.asax / Global.asax.cs    # Application startup
├── packages.config                 # NuGet package references
└── GSTReraReconciliation.csproj    # MSBuild project file
```

---

## 3. Database Design

### 3.1 Entity-Relationship Diagram

```
  ┌──────────────────────┐
  │    UploadSessions    │
  ├──────────────────────┤
  │ PK  Id         INT   │
  │     UploadDate DATETIME│
  └────────┬─────────────┘
           │ 1
           │
     ┌─────┼──────────────────────┐
     │     │                      │
     │ *   │ *                    │ *
┌────┴─────┴────┐   ┌─────────────┴──────────┐  ┌───────────────────────┐
│  RERARecords   │  │    GSTRecords          │  │  ComparisonResults    │
├────────────────┤  ├────────────────────────┤  ├───────────────────────┤
│ PK Id      INT │  │ PK Id         INT      │  │ PK Id          INT    │
│ FK SessionId   │  │ FK SessionId  INT      │  │ FK SessionId   INT    │
│    Name  NV500 │  │    Name     NV500      │  │    RERAName   NV500   │
│    Amount D18,2│  │    GSTAmount D18,2     │  │    GSTName    NV500   │
└────────────────┘  └────────────────────────┘  │    ExpectedGST D18,2  │
                                                │    ActualGST   D18,2  │
                                                │    Status     NV20    │
                                                └───────────────────────┘
```

### 3.2 Table Definitions

#### 3.2.1 UploadSessions

Tracks each file upload session. A session groups one RERA file and one GST file together.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `INT` | PK, IDENTITY(1,1) | Auto-increment primary key |
| `UploadDate` | `DATETIME` | NOT NULL, DEFAULT GETUTCDATE() | Timestamp of upload |

**Indexes:** `IX_UploadSessions_UploadDate` (DESC)

#### 3.2.2 RERARecords

Individual rows parsed from the RERA bank statement Excel file.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `INT` | PK, IDENTITY(1,1) | Auto-increment primary key |
| `SessionId` | `INT` | FK → UploadSessions(Id), CASCADE DELETE | Parent session |
| `Name` | `NVARCHAR(500)` | NOT NULL | Entity/party name from RERA |
| `Amount` | `DECIMAL(18,2)` | NOT NULL | Transaction amount (₹) |

**Indexes:** `IX_RERARecords_SessionId`, `IX_RERARecords_Name`, `IX_RERARecords_Amount`

#### 3.2.3 GSTRecords

Individual rows parsed from the GST return Excel file.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `INT` | PK, IDENTITY(1,1) | Auto-increment primary key |
| `SessionId` | `INT` | FK → UploadSessions(Id), CASCADE DELETE | Parent session |
| `Name` | `NVARCHAR(500)` | NOT NULL | Entity/party name from GST |
| `GSTAmount` | `DECIMAL(18,2)` | NOT NULL | GST amount filed (₹) |

**Indexes:** `IX_GSTRecords_SessionId`, `IX_GSTRecords_Name`, `IX_GSTRecords_GSTAmount`

#### 3.2.4 ComparisonResults

Stores the output of the reconciliation algorithm — one row per comparison pair.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `INT` | PK, IDENTITY(1,1) | Auto-increment primary key |
| `SessionId` | `INT` | FK → UploadSessions(Id), CASCADE DELETE | Parent session |
| `RERAName` | `NVARCHAR(500)` | NULL allowed | Name from RERA (NULL for GST_NOT_RERA) |
| `GSTName` | `NVARCHAR(500)` | NULL allowed | Name from GST (NULL for RERA_NOT_GST) |
| `ExpectedGST` | `DECIMAL(18,2)` | NOT NULL, DEFAULT 0 | RERA Amount × 5% |
| `ActualGST` | `DECIMAL(18,2)` | NOT NULL, DEFAULT 0 | GST amount from filing |
| `NameScore` | `INT` | NOT NULL, DEFAULT 0 | Name match score (0-100) |
| `AmountScore` | `INT` | NOT NULL, DEFAULT 0 | Amount match score (0-100) |
| `FinalScore` | `INT` | NOT NULL, DEFAULT 0 | Combined match score (0-100) |
| `Status` | `NVARCHAR(30)` | NOT NULL | Reconciliation status |

*(Note: The `Status` column was expanded to NVARCHAR(30) to accommodate new statuses like `MISSING_IN_GST`, `MISSING_IN_RERA`, and `LIKELY_MATCH`, with backward compatibility maintained for legacy data.)*

**Indexes:** `IX_ComparisonResults_SessionId`, `IX_ComparisonResults_Status` (with INCLUDE columns), `IX_ComparisonResults_RERAName`, `IX_ComparisonResults_GSTName`

### 3.3 Database View

```sql
vw_ReconciliationSummary
```

Provides per-session aggregate statistics (MatchedCount, ReraNotGstCount, GstNotReraCount, GstMismatchCount, PossibleMatchCount, TotalDifference).

### 3.4 Referential Integrity

All child tables use `ON DELETE CASCADE` — deleting an `UploadSession` automatically removes all associated RERA records, GST records, and comparison results.

---

## 4. Module Description

### 4.1 Upload Module

**Files:** `UploadController.cs`, `ExcelImportService.cs`, `Upload/Index.cshtml`, `UploadViewModel.cs`

**Purpose:** Handles dual file upload (RERA + GST), file validation, Excel parsing, and comparison triggering.

| Feature | Implementation |
|---------|---------------|
| **Dual File Upload** | Side-by-side cards: RERA (left) + GST (right) |
| **File Validation** | Extension (.xlsx/.xls), MIME type, file size (≤10 MB) |
| **Secure Storage** | UUID-renamed files in `~/App_Data/Uploads/`, path traversal protection |
| **Excel Parsing** | EPPlus reads: RERA → (Name, Amount), GST → (Name, GSTAmount) |
| **CSRF Protection** | `@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]` on all POST actions |
| **PRG Pattern** | POST → TempData → Redirect → GET prevents duplicate submissions |
| **Compare Button** | Activates only when both files are uploaded in the same session |
| **Session History** | Displays recent upload sessions with record counts |

**Controller Actions:**

| Action | Method | Description |
|--------|--------|-------------|
| `Index` | GET | Displays the upload form |
| `UploadRERA` | POST | Validates & imports RERA Excel |
| `UploadGST` | POST | Validates & imports GST Excel |
| `Compare` | POST | Triggers the comparison algorithm |

### 4.2 Comparison Module

**Files:** `ComparisonService.cs`, `IComparisonService.cs`

**Purpose:** Core reconciliation engine that matches RERA records against GST records.

**Algorithm — 3-Phase Processing with Scoring:**

```text
Phase 1: Exact Name Match (O(1) with Dictionary)
  → Name matched perfectly?
      Yes: Calculate ExpectedGST. If ExpectedGST == ActualGST → MATCHED (100/100/100)
      Else: GST_MISMATCH (100/calc/calc)

Phase 2: Fuzzy Name Match (FuzzySharp WeightedRatio)
  → For unmatched records: Find highest NameScore.
      If NameScore ≥ 90 and AmountScore ≥ 90 (within 10% diff) → LIKELY_MATCH
      If NameScore ≥ 80 → POSSIBLE_MATCH

Phase 3: Amount-Based Match
  → For still-unmatched records:
      If ExpectedGST == ActualGST (within ₹1 tolerance) → LIKELY_MATCH

Unmatched Records:
  → MISSING_IN_GST (RERA with no counterpart)
  → MISSING_IN_RERA (GST with no counterpart)
```

**Scoring Formula:**
- **NameScore:** FuzzySharp `WeightedRatio` (0–100)
- **AmountScore:** `max(0, 100 - (|expected-actual| / max(expected, 1) * 100))`
- **FinalScore:** `(int)(NameScore * 0.6 + AmountScore * 0.4)`

**Status Definitions:**

| Status | Code | Condition |
|--------|------|-----------|
| Matched | `MATCHED` | Exact name match AND ExpectedGST matches ActualGST |
| Likely Match | `LIKELY_MATCH` | High confidence name match (≥90) + amount match, OR exact amount match |
| Possible Match | `POSSIBLE_MATCH` | Partial name similarity detected (Score ≥80) — needs review |
| GST Mismatch | `GST_MISMATCH` | Exact name match BUT ExpectedGST ≠ ActualGST |
| Missing in GST | `MISSING_IN_GST` | Present in RERA bank statement but not found in GST |
| Missing in RERA | `MISSING_IN_RERA` | Present in GST records but not found in RERA |

**GST Calculation:**
```
Expected GST = RERA Amount × 5%
             = Amount × 0.05
             = Math.Round(amount * 0.05, 2, MidpointRounding.ToEven)
```

### 4.3 Dashboard Module

**Files:** `DashboardController.cs`, `DashboardViewModel.cs`, `Dashboard/Index.cshtml`

**Purpose:** Visual summary of reconciliation results with interactive session filtering.

| Component | Description |
|-----------|-------------|
| **Metric Cards (4)** | Total RERA Records, Total GST Records, Match Rate %, Total GST Difference |
| **Status Cards (6)** | Individual count per status (Matched, Likely, Possible, Mismatch, Missing GST, Missing RERA) |
| **Progress Bar** | Stacked, color-coded proportional breakdown for all 6 statuses |
| **Results Table** | Color-coded rows with score columns, monospace currency |
| **Session Filter** | Dropdown to scope data to a specific upload session |

**EF Query Strategy:** 6 optimized queries (CountAsync for RERA/GST, GroupBy for status breakdown, materialized for difference Sum, Skip/Take for table, GetAll for sessions).

### 4.4 Reports Module

**Files:** `ReportsController.cs`, `ReportFilterViewModel.cs`, `Reports/Index.cshtml`

**Purpose:** Full-featured reporting with filtering, sorting, pagination, search, and export.

| Feature | Implementation |
|---------|---------------|
| **7 Tabs** | All, Matched, Likely Match, Possible Match, GST Mismatch, Missing in GST, Missing in RERA |
| **Search** | SQL `LIKE '%term%'` on RERAName + GSTName via EF `Contains()` |
| **Sorting** | 9 sortable columns including NameScore, AmountScore, and FinalScore |
| **Pagination** | Server-side Skip/Take, 7-page sliding window |
| **Page Size** | Configurable: 10, 25, 50, 100 per page |
| **Session Filter** | Dropdown to scope by session |
| **State Preservation** | All filters preserved across tab switches, sorts, and page changes |

### 4.5 Export Module

**Files:** `ExportService.cs`, `IExportService.cs` (called from `ReportsController`)

**Purpose:** Excel workbook generation using EPPlus with professional formatting.

| Export Type | Action | Output |
|-------------|--------|--------|
| **Export All** | `/Reports/ExportAll` | Multi-sheet: Summary + All Records + status-specific sheets |
| **Matched** | `/Reports/ExportMatched` | Single sheet: Matched Records |
| **Likely Match** | `/Reports/ExportLikelyMatch` | Single sheet: Likely Match |
| **Possible Match** | `/Reports/ExportPossibleMatch` | Single sheet: Possible Match |
| **GST Mismatch** | `/Reports/ExportMismatch` | Single sheet: GST Mismatch |
| **Missing in GST** | `/Reports/ExportMissingInGst` | Single sheet: Missing in GST |
| **Missing in RERA** | `/Reports/ExportMissingInRera` | Single sheet: Missing in RERA |

**Excel Formatting:**

- Dark navy headers (#1A1A2E) with white text and red accent border
- Color-coded rows per status (green/yellow/red/gray/blue)
- Currency formatting: `₹#,##0.00`
- Bold red font for non-zero differences
- Totals row with double-line border
- Frozen header rows
- Auto-fit column widths with minimum widths
- Landscape print layout, fit-to-width

---

## 5. Setup Guide

### 5.1 Prerequisites

| Requirement | Minimum Version |
|-------------|----------------|
| **Operating System** | Windows 10 / Windows Server 2016+ |
| **IDE** | Visual Studio 2019 or later (Community edition or above) |
| **.NET Framework** | 4.8 (Developer Pack) |
| **SQL Server** | 2016 or later (Express edition acceptable) |
| **SQL Server Management Studio** | 18.0+ (recommended for SQL script execution) |
| **IIS** | 10.0 (for deployment; IIS Express for development) |
| **NuGet** | Package Manager (included with Visual Studio) |

### 5.2 Connection String Configuration

The connection string is defined in `Web.config`:

```xml
<connectionStrings>
    <add name="ReconciliationDb"
         connectionString="Data Source=localhost;
                          Initial Catalog=GSTReraReconciliationDb;
                          Integrated Security=True;
                          MultipleActiveResultSets=True"
         providerName="System.Data.SqlClient" />
</connectionStrings>
```

**Configuration Options:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Data Source` | `localhost` | SQL Server instance name (e.g., `.\SQLEXPRESS`) |
| `Initial Catalog` | `GSTReraReconciliationDb` | Database name |
| `Integrated Security` | `True` | Windows Authentication (recommended) |
| `MultipleActiveResultSets` | `True` | Required for async EF queries |

For SQL Server Authentication, replace:
```xml
connectionString="Data Source=YOUR_SERVER;
                 Initial Catalog=GSTReraReconciliationDb;
                 User Id=YOUR_USER;Password=YOUR_PASSWORD;
                 MultipleActiveResultSets=True"
```

---

## 6. Installation Steps

### 6.1 Step-by-Step Installation

**Step 1: Clone/Download the Project**
```
Copy the GSTReraReconciliation folder to your development machine.
```

**Step 2: Open in Visual Studio**
```
Open GSTReraReconciliation.sln in Visual Studio 2019+.
```

**Step 3: Restore NuGet Packages**
```
Right-click Solution → Restore NuGet Packages
```

Or via Package Manager Console:
```powershell
Update-Package -reinstall
```

**Step 4: Verify NuGet Packages**

Ensure the following packages are installed:

| Package | Version |
|---------|---------|
| EntityFramework | 6.5.1 |
| EPPlus | 4.5.3.3 |
| FuzzySharp | 2.0.2 |
| Microsoft.AspNet.Mvc | 5.2.9 |
| Bootstrap | 5.3.3 |
| jQuery | 3.7.1 |
| Newtonsoft.Json | 13.0.3 |

**Step 5: Configure Connection String**
```
Edit Web.config → <connectionStrings> section
Update "Data Source" to match your SQL Server instance.
```

**Step 6: Create Database**
```
Execute SQL\InitialSchema.sql in SQL Server Management Studio.
(See Section 7: SQL Deployment Steps)
```

**Step 7: Build the Project**
```
Build → Build Solution (Ctrl+Shift+B)
Verify: 0 errors
```

**Step 8: Run the Application**
```
Debug → Start Without Debugging (Ctrl+F5)
OR
Press F5 for debug mode.
```

**Step 9: Verify**
```
Browser should open to http://localhost:PORT/
Navigate to Upload page to test file upload.
```

---

## 7. SQL Deployment Steps

### 7.1 Database Creation

**Option A: Using SQL Script (Recommended)**

1. Open **SQL Server Management Studio (SSMS)**
2. Connect to your SQL Server instance
3. Open a **New Query** window
4. First, create the database:

```sql
CREATE DATABASE GSTReraReconciliationDb;
GO
USE GSTReraReconciliationDb;
GO
```

5. Open and execute the file:
```
SQL\InitialSchema.sql
```

6. Verify output:
```
=== GST RERA Reconciliation schema created successfully ===
```

**Option B: Using EF Code First Migrations**

1. Open **Package Manager Console** in Visual Studio
2. Run:
```powershell
Enable-Migrations
Add-Migration InitialCreate
Update-Database
```

### 7.2 Objects Created

| Object Type | Name | Description |
|-------------|------|-------------|
| **Table** | `UploadSessions` | Upload session tracking |
| **Table** | `RERARecords` | RERA bank statement rows |
| **Table** | `GSTRecords` | GST return rows |
| **Table** | `ComparisonResults` | Reconciliation results |
| **View** | `vw_ReconciliationSummary` | Per-session aggregate statistics |
| **Indexes** | 11 indexes | FK lookups, name search, status filtering |
| **Constraints** | 4 FKs, 1 CHECK | Referential integrity + status validation |

### 7.3 Verification Queries

After deployment, run these queries to verify:

```sql
-- Verify all tables exist
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME;

-- Verify foreign keys
SELECT
    fk.name AS FK_Name,
    tp.name AS Parent_Table,
    tr.name AS Referenced_Table
FROM sys.foreign_keys fk
JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id;

-- Verify CHECK constraint
SELECT name, definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('ComparisonResults');

-- Verify view
SELECT * FROM sys.views WHERE name = 'vw_ReconciliationSummary';
```

---

## 8. User Guide

### 8.1 Accessing the Application

Open a browser and navigate to the application URL (e.g., `http://localhost:PORT/`). The landing page provides an overview and navigation links.

### 8.2 Uploading Files

1. Click **"Upload & Compare"** in the navigation menu.
2. **Upload RERA File:**
   - Click the RERA upload card (left side)
   - Select your RERA bank statement Excel file
   - Required columns: **Name** (Column A), **Amount** (Column B)
   - Click **"Upload RERA File"**
   - A green success message shows the number of records imported

3. **Upload GST File:**
   - Click the GST upload card (right side)
   - Select your GST return Excel file
   - Required columns: **Name** (Column A), **GSTAmount** (Column B)
   - Click **"Upload GST File"**
   - A green success message shows the number of records imported

4. **Compare Data:**
   - Once both files are uploaded, the **"Compare Data"** button activates (turns green)
   - Click the button to run the reconciliation algorithm
   - The system redirects to the Dashboard with results

### 8.3 Excel File Format Requirements

**RERA Bank Statement (.xlsx / .xls):**

| Column A | Column B |
|----------|----------|
| **Name** | **Amount** |
| ABC Builders Pvt Ltd | 150000.00 |
| XYZ Infrastructure | 275000.50 |

**GST Return (.xlsx / .xls):**

| Column A | Column B |
|----------|----------|
| **Name** | **GSTAmount** |
| ABC Builders Pvt Ltd | 7500.00 |
| XYZ Infrastructure | 13750.03 |

**Constraints:**
- File type: `.xlsx` or `.xls` only
- Maximum file size: 10 MB
- First row: Header (must contain column names)
- Data starts from Row 2

### 8.4 Viewing the Dashboard

1. Click **"Dashboard"** in the navigation menu.
2. **Top Metric Cards:** Total RERA records, Total GST records, Match Rate %, Total Difference (₹)
3. **Status Cards:** Individual counts for each of the 5 statuses
4. **Progress Bar:** Visual proportional breakdown of all statuses
5. **Results Table:** Detailed row-by-row comparison results with color coding
6. **Session Filter:** Use the dropdown to view results for a specific session

### 8.5 Using Reports

1. Click **"Reports"** in the navigation menu.
2. **Tab Navigation:** Click any tab to filter by status:
   - All | Matched | RERA Not GST | GST Not RERA | GST Mismatch | Possible Match
3. **Search:** Type a name in the search box and click "Apply"
4. **Sort:** Click any column header to sort ascending/descending
5. **Pagination:** Navigate pages using the pagination controls at the bottom
6. **Per Page:** Change the number of records per page (10/25/50/100)

### 8.6 Exporting to Excel

1. On the **Reports** page, click the green **"Export"** button (top right).
2. **Quick Export:** Click the main button to export the current filtered view.
3. **Dropdown Options:**
   - **Export All (Multi-Sheet):** Creates a workbook with Summary + All Records + status-specific sheets
   - **Individual exports:** Matched, RERA Not GST, GST Not RERA, GST Mismatch, Possible Match
4. The Excel file downloads automatically with timestamp in the filename.

---

## 9. Project Flow Diagram

### 9.1 Complete Application Flow

```
  USER                         SYSTEM                           DATABASE
   │                              │                                │
   │  Upload RERA Excel           │                                │
   ├─────────────────────────────►│                                │
   │                              │  Validate file                 │
   │                              │  (ext, size, MIME)             │
   │                              │  Save to App_Data (UUID name)  │
   │                              │  Parse with EPPlus             │
   │                              │  (Name, Amount)                │
   │                              │                                │
   │                              │  INSERT UploadSession          │
   │                              ├───────────────────────────────►│
   │                              │  INSERT RERARecords (batch)    │
   │                              ├───────────────────────────────►│
   │  ◄ Success: N records        │                                │
   │                              │                                │
   │  Upload GST Excel            │                                │
   ├─────────────────────────────►│                                │
   │                              │  Validate + Parse              │
   │                              │  (Name, GSTAmount)             │
   │                              │                                │
   │                              │  INSERT GSTRecords (batch)     │
   │                              ├───────────────────────────────►│
   │  ◄ Success: M records        │                                │
   │                              │                                │
   │  Click "Compare Data"        │                                │
   ├─────────────────────────────►│                                │
   │                              │  SELECT RERARecords            │
   │                              │◄───────────────────────────────┤
   │                              │  SELECT GSTRecords             │
   │                              │◄───────────────────────────────┤
   │                              │                                │
   │                              │  ┌─── Phase 1 ───────────┐    │
   │                              │  │  Build Dictionary      │    │
   │                              │  │  Exact Name Match      │    │
   │                              │  │  Calculate GST (×5%)   │    │
   │                              │  │  → MATCHED/MISMATCH    │    │
   │                              │  └────────────────────────┘    │
   │                              │  ┌─── Phase 2 ───────────┐    │
   │                              │  │  Fuzzy Match (>80%)    │    │
   │                              │  │  FuzzySharp Weighted   │    │
   │                              │  │  → POSSIBLE/RERA_NOT   │    │
   │                              │  └────────────────────────┘    │
   │                              │  ┌─── Phase 3 ───────────┐    │
   │                              │  │  Remaining GST         │    │
   │                              │  │  → GST_NOT_RERA        │    │
   │                              │  └────────────────────────┘    │
   │                              │                                │
   │                              │  INSERT ComparisonResults      │
   │                              ├───────────────────────────────►│
   │                              │                                │
   │  ◄ Redirect to Dashboard     │                                │
   │                              │                                │
   │  View Dashboard              │                                │
   ├─────────────────────────────►│  GROUP BY Status (counts)      │
   │                              ├───────────────────────────────►│
   │  ◄ Summary cards + table     │◄───────────────────────────────┤
   │                              │                                │
   │  View Reports / Export       │                                │
   ├─────────────────────────────►│  SELECT with filters           │
   │                              ├───────────────────────────────►│
   │  ◄ Filtered results / .xlsx  │◄───────────────────────────────┤
   │                              │                                │
```

### 9.2 Comparison Algorithm Flow

```
              ┌─────────────────────────────┐
              │   Load RERA + GST Records    │
              │       for SessionId          │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │  Build GST Dictionary        │
              │  Key: Name.Trim().ToUpper()  │
              │  Value: List<GSTRecord>      │
              └──────────────┬──────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │      FOR EACH RERA Record            │
          │  ┌─────────────────────────────────┐ │
          │  │ Normalize: Name.Trim().ToUpper()│ │
          │  │ Calculate: ExpectedGST = Amt×5% │ │
          │  └───────────────┬─────────────────┘ │
          │                  │                   │
          │       ┌──────────▼──────────┐        │
          │       │ Dictionary.TryGet() │        │
          │       └──────┬──────┬───────┘        │
          │         FOUND│      │NOT FOUND       │
          │              │      │                │
          │    ┌─────────▼─┐  ┌─▼──────────────┐ │
          │    │GST == Exp? │  │Queue for Fuzzy │ │
          │    └──┬─────┬──┘  └────────────────┘ │
          │   YES │     │ NO                     │
          │  ┌────▼──┐┌─▼────────┐               │
          │  │MATCHED││GST_      │               │
          │  │       ││MISMATCH  │               │
          │  └───────┘└──────────┘               │
          └──────────────────────────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │  FOR EACH Unmatched RERA             │
          │  ┌─────────────────────────────────┐ │
          │  │ Fuzz.WeightedRatio() vs all     │ │
          │  │ unmatched GST records           │ │
          │  └───────────────┬─────────────────┘ │
          │                  │                   │
          │       ┌──────────▼──────────┐        │
          │       │  Score > 80?        │        │
          │       └──────┬──────┬───────┘        │
          │          YES │      │ NO             │
          │   ┌──────────▼┐ ┌──▼───────────┐    │
          │   │POSSIBLE_  │ │RERA_NOT_GST  │    │
          │   │MATCH      │ │              │    │
          │   └───────────┘ └──────────────┘    │
          └──────────────────────────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │  Remaining Unmatched GST Records     │
          │           → GST_NOT_RERA             │
          └──────────────────┬──────────────────┘
                             │
          ┌──────────────────▼──────────────────┐
          │  Batch INSERT ComparisonResults      │
          │       (AddRange + SaveChanges)       │
          └─────────────────────────────────────┘
```

---

## 10. Assumptions

### 10.1 Business Assumptions

| # | Assumption |
|---|-----------|
| 1 | GST rate is fixed at **5%** of the RERA transaction amount. |
| 2 | Names in RERA and GST records refer to the same entities (parties/vendors). |
| 3 | Each upload session consists of exactly **one RERA file** and **one GST file**. |
| 4 | The first row of each Excel file contains **column headers**. |
| 5 | RERA file has columns: **Name** (A), **Amount** (B). |
| 6 | GST file has columns: **Name** (A), **GSTAmount** (B). |
| 7 | Name matching is **case-insensitive** (normalized via Trim + ToUpper). |
| 8 | A fuzzy match score **above 80** (out of 100) indicates a likely name match. |
| 9 | Each RERA record matches to **at most one** GST record and vice versa. |
| 10 | Financial amounts use **Indian Rupees (₹)** with 2 decimal places. |

### 10.2 Technical Assumptions

| # | Assumption |
|---|-----------|
| 1 | The application runs on a **Windows Server** with IIS 10+. |
| 2 | SQL Server is accessible from the web server (same machine or network). |
| 3 | **Windows Authentication** is used for database connectivity (recommended). |
| 4 | Excel files are **10 MB or less** in size. |
| 5 | Only **.xlsx** and **.xls** file formats are supported. |
| 6 | The application is accessed from within an **intranet** (single-tenant). |
| 7 | Users do not require **authentication/authorization** (internal tool). |
| 8 | Concurrent uploads by different users target **different sessions**. |
| 9 | Browser supports **Bootstrap 5** (Chrome 90+, Firefox 78+, Edge 90+, Safari 14+). |

### 10.3 Security Assumptions

| # | Assumption |
|---|-----------|
| 1 | Uploaded files are treated as **untrusted** — validated before processing. |
| 2 | Files are stored **outside the web root** (`App_Data/Uploads/`) with UUID names. |
| 3 | All database access uses **EF parameterized queries** (no raw SQL concatenation). |
| 4 | All POST forms include **CSRF tokens** (`AntiForgeryToken`). |
| 5 | Security headers (X-Frame-Options, X-Content-Type-Options, etc.) are enforced. |
| 6 | Custom error pages hide **stack traces** and internal details from users. |

---

## 11. Future Enhancements

### 11.1 Short-Term (Next Release)

| # | Enhancement | Priority | Description |
|---|------------|----------|-------------|
| 1 | **User Authentication** | High | Add ASP.NET Identity with role-based access (Admin, Auditor, Viewer) |
| 2 | **PDF Export** | High | Generate PDF reports using Rotativa or iTextSharp |
| 3 | **Configurable GST Rate** | Medium | Allow admins to set GST rate (currently hardcoded at 5%) |
| 4 | **Audit Logging** | Medium | Track who uploaded files, ran comparisons, and exported reports |
| 5 | **Email Notifications** | Medium | Send summary email after comparison completes |

### 11.2 Medium-Term (Version 2.0)

| # | Enhancement | Priority | Description |
|---|------------|----------|-------------|
| 6 | **Bulk Session Management** | Medium | Archive, delete, or re-run comparisons on old sessions |
| 7 | **Column Mapping UI** | Medium | Allow users to map Excel columns dynamically instead of fixed A/B |
| 8 | **Multiple GST Rates** | Medium | Support different GST slabs (5%, 12%, 18%, 28%) per record type |
| 9 | **Chart Visualizations** | Low | Add Chart.js pie/bar charts to Dashboard for trend analysis |
| 10 | **Data Import from APIs** | Low | Integrate with GST Portal API for direct data pull |

### 11.3 Long-Term (Version 3.0)

| # | Enhancement | Priority | Description |
|---|------------|----------|-------------|
| 11 | **Multi-Tenant Support** | High | Isolate data by company/branch for SaaS deployment |
| 12 | **Automated Scheduling** | Medium | Cron-based recurring reconciliation from shared folders |
| 13 | **Machine Learning Matching** | Low | Train ML model on historical matches for smarter fuzzy matching |
| 14 | **REST API** | Medium | Expose reconciliation endpoints for integration with ERP systems |
| 15 | **Migration to .NET 8** | Low | Upgrade from .NET Framework 4.8 to .NET 8 for cross-platform support |

---

## Appendix A: NuGet Package Reference

| Package | Version | Purpose |
|---------|---------|---------|
| `EntityFramework` | 6.5.1 | ORM for SQL Server database access |
| `EPPlus` | 4.5.3.3 | Excel file reading and writing |
| `FuzzySharp` | 2.0.2 | Fuzzy string matching (Levenshtein-based) |
| `Microsoft.AspNet.Mvc` | 5.2.9 | ASP.NET MVC 5 framework |
| `Microsoft.AspNet.Razor` | 3.2.9 | Razor view engine |
| `Microsoft.AspNet.WebPages` | 3.2.9 | WebPages framework |
| `Microsoft.AspNet.Web.Optimization` | 1.1.3 | CSS/JS bundling and minification |
| `bootstrap` | 5.3.3 | Frontend CSS framework |
| `jQuery` | 3.7.1 | JavaScript library |
| `jQuery.Validation` | 1.20.0 | Client-side form validation |
| `Microsoft.jQuery.Unobtrusive.Validation` | 3.2.12 | Unobtrusive validation adapter |
| `Newtonsoft.Json` | 13.0.3 | JSON serialization |
| `WebGrease` | 1.6.0 | CSS/JS optimization |
| `Microsoft.Web.Infrastructure` | 2.0.0 | Web infrastructure utilities |

## Appendix B: Security Headers

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Frame-Options` | `DENY` | Prevents clickjacking |
| `X-Content-Type-Options` | `nosniff` | Prevents MIME-type sniffing |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Controls referrer info |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Blocks unused APIs |

## Appendix C: File Upload Security

| Check | Implementation |
|-------|---------------|
| Extension whitelist | `.xlsx`, `.xls` only |
| MIME type validation | Checked against expected content types |
| File size limit | 10 MB (enforced in Web.config + application code) |
| File rename | UUID (`Guid.NewGuid()`) — original filename never used on disk |
| Storage location | `~/App_Data/Uploads/` (outside web root, not directly accessible) |
| Path traversal | `Path.GetFullPath()` boundary check against upload directory |

---

*Document prepared for Sactum Infratech — GST vs RERA Bank Statement Reconciliation System v1.0*
