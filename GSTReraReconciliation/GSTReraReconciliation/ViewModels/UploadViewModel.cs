using System.Collections.Generic;
using System.Web;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.ViewModels
{
    /// <summary>
    /// ViewModel for the Upload page.
    /// Supports dual upload (RERA + GST), comparison trigger, and import statistics.
    /// </summary>
    public class UploadViewModel
    {
        // --- RERA Upload ---

        /// <summary>RERA bank statement Excel file.</summary>
        public HttpPostedFileBase RERAFile { get; set; }

        /// <summary>Number of RERA records imported in this session.</summary>
        public int? RERARecordCount { get; set; }

        /// <summary>Status message for RERA upload.</summary>
        public string RERAMessage { get; set; }

        /// <summary>Whether RERA upload succeeded.</summary>
        public bool RERASuccess { get; set; }

        /// <summary>Detailed RERA import statistics.</summary>
        public ImportResult RERAImportResult { get; set; }

        // --- GST Upload ---

        /// <summary>GST return/register Excel file.</summary>
        public HttpPostedFileBase GSTFile { get; set; }

        /// <summary>Number of GST records imported in this session.</summary>
        public int? GSTRecordCount { get; set; }

        /// <summary>Status message for GST upload.</summary>
        public string GSTMessage { get; set; }

        /// <summary>Whether GST upload succeeded.</summary>
        public bool GSTSuccess { get; set; }

        /// <summary>Detailed GST import statistics.</summary>
        public ImportResult GSTImportResult { get; set; }

        // --- Session ---

        /// <summary>Current active session ID (set after first upload).</summary>
        public int? SessionId { get; set; }

        /// <summary>Whether both RERA and GST files have been uploaded for this session.</summary>
        public bool BothUploaded
        {
            get { return RERARecordCount.HasValue && GSTRecordCount.HasValue; }
        }

        // --- Comparison ---

        /// <summary>Status message after comparison.</summary>
        public string CompareMessage { get; set; }

        /// <summary>Whether comparison succeeded.</summary>
        public bool CompareSuccess { get; set; }

        // --- Upload History ---

        /// <summary>Recent upload sessions for the history table.</summary>
        public IEnumerable<UploadSession> RecentSessions { get; set; }

        public UploadViewModel()
        {
            RecentSessions = new List<UploadSession>();
        }
    }
}
