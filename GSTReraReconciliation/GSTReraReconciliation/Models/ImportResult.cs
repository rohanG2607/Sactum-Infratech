namespace GSTReraReconciliation.Models
{
    /// <summary>
    /// DTO that carries import statistics back from the Excel import service.
    /// Displayed in the Upload UI to give users visibility into the import pipeline.
    /// </summary>
    public class ImportResult
    {
        /// <summary>Total rows read from the Excel file (excluding header area).</summary>
        public int TotalRowsRead { get; set; }

        /// <summary>Rows that passed all filters (description not empty, credit > 0, not blocked).</summary>
        public int ValidCustomerRecords { get; set; }

        /// <summary>Rows skipped (empty description, no credit, blocked keyword, etc.).</summary>
        public int SkippedRows { get; set; }

        /// <summary>Records actually persisted to the database.</summary>
        public int ImportedRecords { get; set; }

        /// <summary>1-based row number where the data header was detected in the Excel file.</summary>
        public int HeaderRowDetected { get; set; }
    }
}
