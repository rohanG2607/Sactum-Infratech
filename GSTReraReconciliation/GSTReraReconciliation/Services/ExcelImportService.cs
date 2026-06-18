using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Hosting;
using OfficeOpenXml;
using GSTReraReconciliation.Data.Repositories;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Service for importing real bank statement Excel files (RERA and GST) into the database.
    ///
    /// Workflow per file:
    ///   1. Save uploaded file securely (UUID name, App_Data/Uploads)
    ///   2. Open with EPPlus
    ///   3. Auto-detect the header row (scans for "Transaction Date" + "Description" + "Credit")
    ///   4. Map column indices dynamically from the header row
    ///   5. Read data rows from headerRow+1 onward
    ///   6. For each row:
    ///      a. Skip if Description empty
    ///      b. Skip if Credit empty or &lt;= 0
    ///      c. Skip if blocked by TransactionFilterService
    ///      d. Extract customer name via NameExtractionService
    ///      e. Normalize name (Trim, ToUpper, collapse spaces)
    ///      f. Create record with Name, Amount/GSTAmount = Credit, OriginalDescription
    ///   7. Save to database
    ///   8. Return ImportResult with statistics
    ///
    /// Security:
    ///   - File extension allow-list (.xlsx, .xls only)
    ///   - 10 MB file size limit
    ///   - UUID-renamed storage outside web root (App_Data/Uploads)
    ///   - Path traversal protection via resolved path boundary check
    ///   - String truncation to respect column length constraints
    /// </summary>
    public class ExcelImportService : IExcelImportService
    {
        private readonly IGenericRepository<RERARecord> _reraRepository;
        private readonly IGenericRepository<GSTRecord> _gstRepository;
        private readonly NameExtractionService _nameExtractor;
        private readonly TransactionFilterService _transactionFilter;

        // --- Constants ---
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly HashSet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls" };

        // Column header names to detect (case-insensitive)
        private const string HeaderTransactionDate = "TRANSACTION DATE";
        private const string HeaderDescription = "DESCRIPTION";
        private const string HeaderCredit = "CREDIT";

        public ExcelImportService(
            IGenericRepository<RERARecord> reraRepository,
            IGenericRepository<GSTRecord> gstRepository,
            NameExtractionService nameExtractor,
            TransactionFilterService transactionFilter)
        {
            _reraRepository = reraRepository ?? throw new ArgumentNullException(nameof(reraRepository));
            _gstRepository = gstRepository ?? throw new ArgumentNullException(nameof(gstRepository));
            _nameExtractor = nameExtractor ?? throw new ArgumentNullException(nameof(nameExtractor));
            _transactionFilter = transactionFilter ?? throw new ArgumentNullException(nameof(transactionFilter));
        }

        // ================================================================
        // Validation
        // ================================================================

        /// <inheritdoc />
        public string ValidateFile(string fileName, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "File name is required.";
            }

            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return "Invalid file type. Only .xlsx and .xls files are allowed.";
            }

            if (fileSize <= 0)
            {
                return "The uploaded file is empty.";
            }

            if (fileSize > MaxFileSizeBytes)
            {
                return "File exceeds the maximum allowed size of 10 MB.";
            }

            return null; // Valid
        }

        // ================================================================
        // RERA Import
        // ================================================================

        /// <inheritdoc />
        public async Task<ImportResult> ImportRERARecordsAsync(Stream fileStream, string fileName, int sessionId)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

            string savedPath = SaveFileSecurely(fileStream, fileName);
            var result = new ImportResult();
            var records = ParseBankStatement<RERARecord>(savedPath, sessionId, result,
                (name, amount, description, sid) => new RERARecord
                {
                    SessionId = sid,
                    Name = Truncate(name, 500),
                    Amount = amount,
                    OriginalDescription = Truncate(description, 1000)
                });

            if (records.Count == 0)
            {
                throw new InvalidOperationException(
                    "No valid customer records found in the RERA bank statement. " +
                    "Ensure the file has columns: Transaction Date, Description, Credit. " +
                    "Header row was " + (result.HeaderRowDetected > 0 ? "detected at row " + result.HeaderRowDetected : "NOT detected") + ".");
            }

            _reraRepository.AddRange(records);
            await _reraRepository.SaveChangesAsync();

            result.ImportedRecords = records.Count;
            return result;
        }

        // ================================================================
        // GST Import
        // ================================================================

        /// <inheritdoc />
        public async Task<ImportResult> ImportGSTRecordsAsync(Stream fileStream, string fileName, int sessionId)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

            string savedPath = SaveFileSecurely(fileStream, fileName);
            var result = new ImportResult();
            var records = ParseBankStatement<GSTRecord>(savedPath, sessionId, result,
                (name, amount, description, sid) => new GSTRecord
                {
                    SessionId = sid,
                    Name = Truncate(name, 500),
                    GSTAmount = amount,
                    OriginalDescription = Truncate(description, 1000)
                });

            if (records.Count == 0)
            {
                throw new InvalidOperationException(
                    "No valid customer records found in the GST bank statement. " +
                    "Ensure the file has columns: Transaction Date, Description, Credit. " +
                    "Header row was " + (result.HeaderRowDetected > 0 ? "detected at row " + result.HeaderRowDetected : "NOT detected") + ".");
            }

            _gstRepository.AddRange(records);
            await _gstRepository.SaveChangesAsync();

            result.ImportedRecords = records.Count;
            return result;
        }

        // ================================================================
        // Core Parsing Logic
        // ================================================================

        /// <summary>
        /// Generic bank statement parser that works for both RERA and GST records.
        /// Auto-detects the header row, maps columns, filters, extracts names.
        /// </summary>
        /// <typeparam name="T">The entity type (RERARecord or GSTRecord).</typeparam>
        /// <param name="filePath">Path to the saved Excel file.</param>
        /// <param name="sessionId">Session ID to associate records with.</param>
        /// <param name="result">ImportResult to populate with statistics.</param>
        /// <param name="createRecord">Factory function to create the entity.</param>
        private List<T> ParseBankStatement<T>(
            string filePath,
            int sessionId,
            ImportResult result,
            Func<string, decimal, string, int, T> createRecord) where T : class
        {
            var records = new List<T>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets[1]; // First worksheet (1-indexed in EPPlus 4.x)
                if (ws == null)
                {
                    throw new InvalidOperationException("The Excel file does not contain any worksheets.");
                }

                int totalRows = ws.Dimension != null ? ws.Dimension.Rows : 0;
                int totalCols = ws.Dimension != null ? ws.Dimension.Columns : 0;

                if (totalRows < 2)
                {
                    throw new InvalidOperationException("The Excel file has no data rows.");
                }

                // --- Step 1: Auto-detect header row ---
                int headerRow = -1;
                int descriptionCol = -1;
                int creditCol = -1;

                for (int row = 1; row <= Math.Min(totalRows, 50); row++) // Scan first 50 rows max
                {
                    for (int col = 1; col <= totalCols; col++)
                    {
                        string cellText = GetCellText(ws, row, col);
                        if (cellText.Equals(HeaderTransactionDate, StringComparison.OrdinalIgnoreCase) ||
                            cellText.Equals("TXN DATE", StringComparison.OrdinalIgnoreCase) ||
                            cellText.Equals("TRANS DATE", StringComparison.OrdinalIgnoreCase) ||
                            cellText.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                        {
                            // Found a potential header row — now verify Description and Credit exist
                            int descCol = FindColumnInRow(ws, row, totalCols, HeaderDescription);
                            int credCol = FindColumnInRow(ws, row, totalCols, HeaderCredit);

                            if (descCol > 0 && credCol > 0)
                            {
                                headerRow = row;
                                descriptionCol = descCol;
                                creditCol = credCol;
                                break;
                            }
                        }
                    }
                    if (headerRow > 0) break;
                }

                if (headerRow < 0)
                {
                    throw new InvalidOperationException(
                        "Could not detect the header row in the bank statement. " +
                        "Expected a row containing 'Transaction Date', 'Description', and 'Credit' columns.");
                }

                result.HeaderRowDetected = headerRow;

                // --- Step 2: Read data rows ---
                int dataStart = headerRow + 1;
                int skipped = 0;
                int validCustomer = 0;

                for (int row = dataStart; row <= totalRows; row++)
                {
                    result.TotalRowsRead++;

                    string description = GetCellText(ws, row, descriptionCol);
                    string creditText = GetCellText(ws, row, creditCol);

                    // Skip if description is empty
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        skipped++;
                        continue;
                    }

                    // Skip if credit is empty or <= 0
                    decimal creditAmount = ParseDecimalSafe(creditText);
                    if (creditAmount <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    // Skip non-customer transactions
                    if (!_transactionFilter.IsCustomerTransaction(description))
                    {
                        skipped++;
                        continue;
                    }

                    // Extract customer name
                    string customerName = _nameExtractor.ExtractName(description);
                    if (string.IsNullOrWhiteSpace(customerName))
                    {
                        skipped++;
                        continue;
                    }

                    validCustomer++;

                    records.Add(createRecord(customerName, creditAmount, description, sessionId));
                }

                result.SkippedRows = skipped;
                result.ValidCustomerRecords = validCustomer;
            }

            return records;
        }

        /// <summary>
        /// Finds a column in a specific row by searching for a header name (case-insensitive).
        /// Returns the 1-based column index, or -1 if not found.
        /// </summary>
        private static int FindColumnInRow(ExcelWorksheet ws, int row, int totalCols, string headerName)
        {
            for (int col = 1; col <= totalCols; col++)
            {
                string cellText = GetCellText(ws, row, col);
                if (cellText.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return col;
                }
            }
            return -1;
        }

        /// <summary>
        /// Safely gets trimmed text from a cell, returning empty string for null/empty cells.
        /// </summary>
        private static string GetCellText(ExcelWorksheet ws, int row, int col)
        {
            var cell = ws.Cells[row, col];
            if (cell == null || cell.Value == null) return string.Empty;
            return cell.Text != null ? cell.Text.Trim() : cell.Value.ToString().Trim();
        }

        // ================================================================
        // Secure File Storage
        // ================================================================

        /// <summary>
        /// Saves the uploaded file with a UUID-generated name in App_Data/Uploads (outside web root).
        /// Validates the resolved path stays within the upload directory to prevent path traversal.
        /// </summary>
        private string SaveFileSecurely(Stream fileStream, string originalFileName)
        {
            string uploadDir = HostingEnvironment.MapPath("~/App_Data/Uploads")
                               ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Uploads");

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Rename to UUID — never use original file name in path
            string extension = Path.GetExtension(originalFileName);
            string safeFileName = Guid.NewGuid().ToString("N") + extension;
            string fullPath = Path.Combine(uploadDir, safeFileName);

            // Path traversal protection: verify resolved path is within upload directory
            string resolvedPath = Path.GetFullPath(fullPath);
            string resolvedDir = Path.GetFullPath(uploadDir + Path.DirectorySeparatorChar);
            if (!resolvedPath.StartsWith(resolvedDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path traversal attempt detected.");
            }

            // Write file
            fileStream.Position = 0;
            using (var fs = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write))
            {
                fileStream.CopyTo(fs);
            }

            return resolvedPath;
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// Safely parses a decimal value from cell text. Returns 0 for empty/whitespace or unparseable values.
        /// </summary>
        private static decimal ParseDecimalSafe(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            // Remove currency symbols, commas, spaces, and "CR"/"DR" suffixes
            string cleaned = text
                .Replace("₹", "")
                .Replace(",", "")
                .Replace(" ", "")
                .Trim();

            // Remove CR/DR suffix (e.g., "350252.12CR")
            if (cleaned.EndsWith("CR", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 2);
            if (cleaned.EndsWith("DR", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 2);

            if (decimal.TryParse(cleaned, out decimal result))
            {
                return result;
            }

            return 0m; // Unparseable → treat as zero (skip logic handles this)
        }

        /// <summary>
        /// Truncates a string to the specified max length to respect database column constraints.
        /// </summary>
        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }
    }
}
