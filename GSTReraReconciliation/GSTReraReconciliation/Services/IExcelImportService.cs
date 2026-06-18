using System.IO;
using System.Threading.Tasks;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Service interface for importing RERA and GST bank statement Excel files.
    /// Handles auto-detection of header rows, name extraction from descriptions,
    /// filtering of non-customer transactions, and import statistics.
    /// </summary>
    public interface IExcelImportService
    {
        /// <summary>
        /// Imports RERA bank statement records from an Excel file.
        /// Auto-detects the header row, extracts customer names from descriptions,
        /// filters non-customer transactions, and uses Credit as the amount.
        /// </summary>
        /// <param name="fileStream">The uploaded file stream.</param>
        /// <param name="fileName">Original file name (for validation only — never used in paths).</param>
        /// <param name="sessionId">The upload session to associate records with.</param>
        /// <returns>ImportResult with full statistics.</returns>
        Task<ImportResult> ImportRERARecordsAsync(Stream fileStream, string fileName, int sessionId);

        /// <summary>
        /// Imports GST bank statement records from an Excel file.
        /// Auto-detects the header row, extracts customer names from descriptions,
        /// filters non-customer transactions, and uses Credit as the GSTAmount.
        /// </summary>
        /// <param name="fileStream">The uploaded file stream.</param>
        /// <param name="fileName">Original file name (for validation only — never used in paths).</param>
        /// <param name="sessionId">The upload session to associate records with.</param>
        /// <returns>ImportResult with full statistics.</returns>
        Task<ImportResult> ImportGSTRecordsAsync(Stream fileStream, string fileName, int sessionId);

        /// <summary>
        /// Validates that the file has an allowed extension and is within size limits.
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <param name="fileSize">File size in bytes.</param>
        /// <returns>Null if valid; error message string if invalid.</returns>
        string ValidateFile(string fileName, long fileSize);
    }
}
