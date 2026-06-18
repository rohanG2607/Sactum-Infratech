using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using GSTReraReconciliation.Data.Repositories;
using GSTReraReconciliation.Models;
using GSTReraReconciliation.Services;
using GSTReraReconciliation.ViewModels;

namespace GSTReraReconciliation.Controllers
{
    /// <summary>
    /// Controller for uploading RERA and GST bank statement Excel files and triggering comparison.
    /// All POST actions protected with [ValidateAntiForgeryToken].
    /// </summary>
    public class UploadController : Controller
    {
        private readonly IExcelImportService _importService;
        private readonly IComparisonService _comparisonService;
        private readonly IGenericRepository<UploadSession> _sessionRepository;

        public UploadController(
            IExcelImportService importService,
            IComparisonService comparisonService,
            IGenericRepository<UploadSession> sessionRepository)
        {
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        }

        // ================================================================
        // GET: /Upload
        // ================================================================

        /// <summary>
        /// Displays the upload page with dual upload forms and recent session history.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> Index()
        {
            var model = new UploadViewModel
            {
                RecentSessions = await GetRecentSessionsAsync()
            };

            // Restore session state from TempData (survives redirects)
            if (TempData["SessionId"] != null) model.SessionId = (int)TempData["SessionId"];
            if (TempData["RERARecordCount"] != null) model.RERARecordCount = (int)TempData["RERARecordCount"];
            if (TempData["GSTRecordCount"] != null) model.GSTRecordCount = (int)TempData["GSTRecordCount"];
            if (TempData["RERAMessage"] != null) model.RERAMessage = (string)TempData["RERAMessage"];
            if (TempData["RERASuccess"] != null) model.RERASuccess = (bool)TempData["RERASuccess"];
            if (TempData["GSTMessage"] != null) model.GSTMessage = (string)TempData["GSTMessage"];
            if (TempData["GSTSuccess"] != null) model.GSTSuccess = (bool)TempData["GSTSuccess"];
            if (TempData["CompareMessage"] != null) model.CompareMessage = (string)TempData["CompareMessage"];
            if (TempData["CompareSuccess"] != null) model.CompareSuccess = (bool)TempData["CompareSuccess"];

            // Restore import statistics
            if (TempData["RERAImportResult"] != null) model.RERAImportResult = (ImportResult)TempData["RERAImportResult"];
            if (TempData["GSTImportResult"] != null) model.GSTImportResult = (ImportResult)TempData["GSTImportResult"];

            return View(model);
        }

        // ================================================================
        // POST: /Upload/UploadRERA
        // ================================================================

        /// <summary>
        /// Handles RERA bank statement Excel file upload. Creates a new session if one doesn't exist yet.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadRERA(UploadViewModel model)
        {
            int sessionId = model.SessionId ?? 0;

            try
            {
                // Validate file
                if (model.RERAFile == null || model.RERAFile.ContentLength == 0)
                {
                    SetTempData(sessionId, model, "Please select a RERA bank statement Excel file.", false, "RERA");
                    return RedirectToAction("Index");
                }

                string validationError = _importService.ValidateFile(model.RERAFile.FileName, model.RERAFile.ContentLength);
                if (validationError != null)
                {
                    SetTempData(sessionId, model, validationError, false, "RERA");
                    return RedirectToAction("Index");
                }

                // Create session if needed
                if (sessionId == 0)
                {
                    var session = new UploadSession { UploadDate = DateTime.UtcNow };
                    _sessionRepository.Add(session);
                    await _sessionRepository.SaveChangesAsync();
                    sessionId = session.Id;
                }

                // Import RERA records
                ImportResult importResult;
                using (var stream = model.RERAFile.InputStream)
                {
                    importResult = await _importService.ImportRERARecordsAsync(stream, model.RERAFile.FileName, sessionId);
                }

                // Persist state
                TempData["SessionId"] = sessionId;
                TempData["RERARecordCount"] = importResult.ImportedRecords;
                TempData["RERAImportResult"] = importResult;
                TempData["RERAMessage"] = string.Format(
                    "Successfully imported {0} customer records (Header detected at row {1}, {2} total rows read, {3} skipped).",
                    importResult.ImportedRecords,
                    importResult.HeaderRowDetected,
                    importResult.TotalRowsRead,
                    importResult.SkippedRows);
                TempData["RERASuccess"] = true;

                // Carry forward GST state if already uploaded
                if (model.GSTRecordCount.HasValue)
                {
                    TempData["GSTRecordCount"] = model.GSTRecordCount.Value;
                    TempData["GSTMessage"] = model.GSTMessage;
                    TempData["GSTSuccess"] = model.GSTSuccess;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("RERA upload failed: " + ex.ToString());
                SetTempData(sessionId, model,
                    "Error importing RERA bank statement: " + ex.Message,
                    false, "RERA");
            }

            return RedirectToAction("Index");
        }

        // ================================================================
        // POST: /Upload/UploadGST
        // ================================================================

        /// <summary>
        /// Handles GST bank statement Excel file upload. Creates a new session if one doesn't exist yet.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadGST(UploadViewModel model)
        {
            int sessionId = model.SessionId ?? 0;

            try
            {
                // Validate file
                if (model.GSTFile == null || model.GSTFile.ContentLength == 0)
                {
                    SetTempData(sessionId, model, "Please select a GST bank statement Excel file.", false, "GST");
                    return RedirectToAction("Index");
                }

                string validationError = _importService.ValidateFile(model.GSTFile.FileName, model.GSTFile.ContentLength);
                if (validationError != null)
                {
                    SetTempData(sessionId, model, validationError, false, "GST");
                    return RedirectToAction("Index");
                }

                // Create session if needed
                if (sessionId == 0)
                {
                    var session = new UploadSession { UploadDate = DateTime.UtcNow };
                    _sessionRepository.Add(session);
                    await _sessionRepository.SaveChangesAsync();
                    sessionId = session.Id;
                }

                // Import GST records
                ImportResult importResult;
                using (var stream = model.GSTFile.InputStream)
                {
                    importResult = await _importService.ImportGSTRecordsAsync(stream, model.GSTFile.FileName, sessionId);
                }

                // Persist state
                TempData["SessionId"] = sessionId;
                TempData["GSTRecordCount"] = importResult.ImportedRecords;
                TempData["GSTImportResult"] = importResult;
                TempData["GSTMessage"] = string.Format(
                    "Successfully imported {0} customer records (Header detected at row {1}, {2} total rows read, {3} skipped).",
                    importResult.ImportedRecords,
                    importResult.HeaderRowDetected,
                    importResult.TotalRowsRead,
                    importResult.SkippedRows);
                TempData["GSTSuccess"] = true;

                // Carry forward RERA state if already uploaded
                if (model.RERARecordCount.HasValue)
                {
                    TempData["RERARecordCount"] = model.RERARecordCount.Value;
                    TempData["RERAMessage"] = model.RERAMessage;
                    TempData["RERASuccess"] = model.RERASuccess;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("GST upload failed: " + ex.ToString());
                SetTempData(sessionId, model,
                    "Error importing GST bank statement: " + ex.Message,
                    false, "GST");
            }

            return RedirectToAction("Index");
        }

        // ================================================================
        // POST: /Upload/Compare
        // ================================================================

        /// <summary>
        /// Triggers comparison of RERA and GST records for the current session.
        /// Requires both RERA and GST files to have been uploaded.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Compare(int? sessionId)
        {
            if (!sessionId.HasValue || sessionId.Value == 0)
            {
                TempData["CompareMessage"] = "No active session. Please upload both files first.";
                TempData["CompareSuccess"] = false;
                return RedirectToAction("Index");
            }

            try
            {
                var results = await _comparisonService.RunComparisonAsync(sessionId.Value);
                int resultCount = results.Count();

                TempData["CompareMessage"] = string.Format("Comparison completed — {0} results generated.", resultCount);
                TempData["CompareSuccess"] = true;

                // Redirect to Dashboard to see results
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Comparison failed: " + ex.ToString());
                TempData["SessionId"] = sessionId.Value;
                TempData["CompareMessage"] = "An error occurred during comparison. Please try again.";
                TempData["CompareSuccess"] = false;
                return RedirectToAction("Index");
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        /// <summary>
        /// Sets TempData for status messages that survive the POST → Redirect → GET pattern.
        /// </summary>
        private void SetTempData(int sessionId, UploadViewModel model, string message, bool success, string type)
        {
            if (sessionId > 0) TempData["SessionId"] = sessionId;

            if (type == "RERA")
            {
                TempData["RERAMessage"] = message;
                TempData["RERASuccess"] = success;
                // Carry forward GST state
                if (model.GSTRecordCount.HasValue)
                {
                    TempData["GSTRecordCount"] = model.GSTRecordCount.Value;
                    TempData["GSTMessage"] = model.GSTMessage;
                    TempData["GSTSuccess"] = model.GSTSuccess;
                }
            }
            else
            {
                TempData["GSTMessage"] = message;
                TempData["GSTSuccess"] = success;
                // Carry forward RERA state
                if (model.RERARecordCount.HasValue)
                {
                    TempData["RERARecordCount"] = model.RERARecordCount.Value;
                    TempData["RERAMessage"] = model.RERAMessage;
                    TempData["RERASuccess"] = model.RERASuccess;
                }
            }
        }

        /// <summary>
        /// Gets the 10 most recent upload sessions.
        /// </summary>
        private async Task<System.Collections.Generic.IEnumerable<UploadSession>> GetRecentSessionsAsync()
        {
            return (await _sessionRepository.GetAllAsync())
                .OrderByDescending(s => s.UploadDate)
                .Take(10);
        }
    }
}
