using System.Collections.Generic;
using System.Threading.Tasks;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Service interface for exporting comparison results to Excel.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Exports all comparison results into a single Excel file with multiple worksheets.
        /// Creates one worksheet per status type plus a "Summary" sheet.
        /// </summary>
        /// <param name="results">All comparison results to export.</param>
        /// <returns>Byte array of the generated .xlsx file.</returns>
        Task<byte[]> ExportAllToExcelAsync(IEnumerable<ComparisonResult> results);

        /// <summary>
        /// Exports a filtered set of comparison results into a single-sheet Excel file.
        /// Used when exporting a specific report tab (e.g., only MATCHED records).
        /// </summary>
        /// <param name="results">Filtered comparison results to export.</param>
        /// <param name="sheetName">Name for the worksheet.</param>
        /// <returns>Byte array of the generated .xlsx file.</returns>
        Task<byte[]> ExportToExcelAsync(IEnumerable<ComparisonResult> results, string sheetName = "Report");
    }
}
