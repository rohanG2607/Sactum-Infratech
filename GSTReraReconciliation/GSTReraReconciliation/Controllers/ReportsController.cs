using System;
using System.Data.Entity;
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
    /// Controller for the Reports module.
    /// Provides filtered, paginated, sorted, and searchable views of comparison results
    /// across 6 report types: Matched, Likely Match, Possible Match, GST Mismatch, Missing in GST, Missing in RERA.
    /// Also provides Excel export actions.
    /// </summary>
    public class ReportsController : Controller
    {
        private readonly IGenericRepository<ComparisonResult> _resultRepository;
        private readonly IGenericRepository<UploadSession> _sessionRepository;
        private readonly IExportService _exportService;

        private const int DefaultPageSize = 25;
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public ReportsController(
            IGenericRepository<ComparisonResult> resultRepository,
            IGenericRepository<UploadSession> sessionRepository,
            IExportService exportService)
        {
            _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        }

        // ================================================================
        // GET: /Reports
        // ================================================================

        [HttpGet]
        public async Task<ActionResult> Index(
            string statusFilter,
            string search,
            string sortBy,
            string sortDir,
            int? page,
            int? pageSize,
            int? sessionId)
        {
            // --- Defaults ---
            int currentPage = Math.Max(page ?? 1, 1);
            int currentPageSize = Math.Min(Math.Max(pageSize ?? DefaultPageSize, 10), 100);
            string currentSortBy = string.IsNullOrWhiteSpace(sortBy) ? "RERAName" : sortBy.Trim();
            string currentSortDir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

            // ================================================================
            // 1. Base query
            // ================================================================
            IQueryable<ComparisonResult> baseQuery = _resultRepository.Find(r => true);

            if (sessionId.HasValue && sessionId.Value > 0)
            {
                baseQuery = baseQuery.Where(r => r.SessionId == sessionId.Value);
            }

            // ================================================================
            // 2. Tab badge counts
            // ================================================================
            var statusGroups = await baseQuery
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int allCount = statusGroups.Sum(g => g.Count);
            int matchedCount = statusGroups.Where(g => g.Status == ReconciliationStatus.Matched).Select(g => g.Count).FirstOrDefault();
            int likelyMatchCount = statusGroups.Where(g => g.Status == ReconciliationStatus.LikelyMatch).Select(g => g.Count).FirstOrDefault();
            int possibleMatchCount = statusGroups.Where(g => g.Status == ReconciliationStatus.PossibleMatch).Select(g => g.Count).FirstOrDefault();
            int gstMismatchCount = statusGroups.Where(g => g.Status == ReconciliationStatus.GstMismatch).Select(g => g.Count).FirstOrDefault();
            // Combine new + legacy
            int missingInGstCount = statusGroups.Where(g => g.Status == ReconciliationStatus.MissingInGst || g.Status == ReconciliationStatus.ReraNotGst).Sum(g => g.Count);
            int missingInReraCount = statusGroups.Where(g => g.Status == ReconciliationStatus.MissingInRera || g.Status == ReconciliationStatus.GstNotRera).Sum(g => g.Count);

            // ================================================================
            // 3. Apply status filter (handle both new and legacy)
            // ================================================================
            IQueryable<ComparisonResult> filteredQuery = baseQuery;

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                // Map new display statuses to also include legacy values
                if (statusFilter == ReconciliationStatus.MissingInGst)
                {
                    filteredQuery = filteredQuery.Where(r => r.Status == ReconciliationStatus.MissingInGst || r.Status == ReconciliationStatus.ReraNotGst);
                }
                else if (statusFilter == ReconciliationStatus.MissingInRera)
                {
                    filteredQuery = filteredQuery.Where(r => r.Status == ReconciliationStatus.MissingInRera || r.Status == ReconciliationStatus.GstNotRera);
                }
                else if (ReconciliationStatus.IsValid(statusFilter))
                {
                    filteredQuery = filteredQuery.Where(r => r.Status == statusFilter);
                }
            }

            // ================================================================
            // 4. Apply search
            // ================================================================
            string searchTerm = search != null ? search.Trim() : null;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filteredQuery = filteredQuery.Where(r =>
                    (r.RERAName != null && r.RERAName.Contains(searchTerm)) ||
                    (r.GSTName != null && r.GSTName.Contains(searchTerm)));
            }

            // ================================================================
            // 5. Count, Sort, Page
            // ================================================================
            int totalRecords = await filteredQuery.CountAsync();

            IOrderedQueryable<ComparisonResult> sortedQuery = ApplySorting(filteredQuery, currentSortBy, currentSortDir);

            int totalPages = totalRecords > 0
                ? (int)Math.Ceiling((double)totalRecords / currentPageSize)
                : 1;
            currentPage = Math.Min(currentPage, totalPages);

            var pagedResults = await sortedQuery
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync();

            // ================================================================
            // 6. Load sessions
            // ================================================================
            var sessions = (await _sessionRepository.GetAllAsync())
                .OrderByDescending(s => s.UploadDate)
                .Take(20);

            // ================================================================
            // 7. Build ViewModel
            // ================================================================
            var model = new ReportFilterViewModel
            {
                StatusFilter = statusFilter,
                Search = searchTerm,
                SessionId = sessionId,
                SortBy = currentSortBy,
                SortDir = currentSortDir,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalRecords = totalRecords,
                AllCount = allCount,
                MatchedCount = matchedCount,
                LikelyMatchCount = likelyMatchCount,
                PossibleMatchCount = possibleMatchCount,
                GstMismatchCount = gstMismatchCount,
                MissingInGstCount = missingInGstCount,
                MissingInReraCount = missingInReraCount,
                Results = pagedResults,
                Sessions = sessions
            };

            return View(model);
        }

        // ================================================================
        // EXPORT ACTIONS
        // ================================================================

        [HttpGet]
        public async Task<ActionResult> ExportAll(int? sessionId)
        {
            return await ExecuteExportAsync(null, null, sessionId, null, "Reconciliation_All");
        }

        [HttpGet]
        public async Task<ActionResult> ExportMatched(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.Matched, search, sessionId, "Matched Records", "Reconciliation_Matched");
        }

        [HttpGet]
        public async Task<ActionResult> ExportLikelyMatch(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.LikelyMatch, search, sessionId, "Likely Match", "Reconciliation_Likely_Match");
        }

        [HttpGet]
        public async Task<ActionResult> ExportPossibleMatch(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.PossibleMatch, search, sessionId, "Possible Match", "Reconciliation_Possible_Match");
        }

        [HttpGet]
        public async Task<ActionResult> ExportMismatch(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.GstMismatch, search, sessionId, "GST Mismatch", "Reconciliation_GST_Mismatch");
        }

        [HttpGet]
        public async Task<ActionResult> ExportMissingInGst(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.MissingInGst, search, sessionId, "Missing in GST", "Reconciliation_Missing_In_GST");
        }

        [HttpGet]
        public async Task<ActionResult> ExportMissingInRera(string search, int? sessionId)
        {
            return await ExecuteExportAsync(ReconciliationStatus.MissingInRera, search, sessionId, "Missing in RERA", "Reconciliation_Missing_In_RERA");
        }

        [HttpGet]
        public async Task<ActionResult> ExportExcel(string statusFilter, string search, int? sessionId)
        {
            string sheetName = string.IsNullOrWhiteSpace(statusFilter) ? null : FormatSheetName(statusFilter);
            string filePrefix = string.IsNullOrWhiteSpace(statusFilter) ? "Reconciliation_All" : "Reconciliation_" + statusFilter;
            return await ExecuteExportAsync(statusFilter, search, sessionId, sheetName, filePrefix);
        }

        // ================================================================
        // Shared Export Logic
        // ================================================================

        private async Task<ActionResult> ExecuteExportAsync(
            string statusFilter, string search, int? sessionId,
            string sheetName, string filePrefix)
        {
            try
            {
                IQueryable<ComparisonResult> query = _resultRepository.Find(r => true);

                if (sessionId.HasValue && sessionId.Value > 0)
                {
                    query = query.Where(r => r.SessionId == sessionId.Value);
                }

                if (!string.IsNullOrWhiteSpace(statusFilter))
                {
                    // Handle combined legacy + new statuses
                    if (statusFilter == ReconciliationStatus.MissingInGst)
                    {
                        query = query.Where(r => r.Status == ReconciliationStatus.MissingInGst || r.Status == ReconciliationStatus.ReraNotGst);
                    }
                    else if (statusFilter == ReconciliationStatus.MissingInRera)
                    {
                        query = query.Where(r => r.Status == ReconciliationStatus.MissingInRera || r.Status == ReconciliationStatus.GstNotRera);
                    }
                    else if (ReconciliationStatus.IsValid(statusFilter))
                    {
                        query = query.Where(r => r.Status == statusFilter);
                    }
                }

                string searchTerm = search != null ? search.Trim() : null;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(r =>
                        (r.RERAName != null && r.RERAName.Contains(searchTerm)) ||
                        (r.GSTName != null && r.GSTName.Contains(searchTerm)));
                }

                var results = await query
                    .OrderBy(r => r.Status)
                    .ThenBy(r => r.RERAName)
                    .ToListAsync();

                if (results.Count == 0)
                {
                    TempData["ErrorMessage"] = "No records to export with the selected filters.";
                    return RedirectToAction("Index", new { statusFilter, search, sessionId });
                }

                byte[] fileBytes;
                if (string.IsNullOrEmpty(sheetName))
                {
                    fileBytes = await _exportService.ExportAllToExcelAsync(results);
                }
                else
                {
                    fileBytes = await _exportService.ExportToExcelAsync(results, sheetName);
                }

                string fileName = filePrefix + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                return File(fileBytes, ExcelContentType, fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Excel export failed: " + ex.ToString());
                TempData["ErrorMessage"] = "An error occurred while generating the Excel export. Please try again.";
                return RedirectToAction("Index", new { statusFilter, search, sessionId });
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static IOrderedQueryable<ComparisonResult> ApplySorting(
            IQueryable<ComparisonResult> query, string sortBy, string sortDir)
        {
            switch (sortBy)
            {
                case "GSTName":
                    return sortDir == "asc" ? query.OrderBy(r => r.GSTName) : query.OrderByDescending(r => r.GSTName);
                case "ExpectedGST":
                    return sortDir == "asc" ? query.OrderBy(r => r.ExpectedGST) : query.OrderByDescending(r => r.ExpectedGST);
                case "ActualGST":
                    return sortDir == "asc" ? query.OrderBy(r => r.ActualGST) : query.OrderByDescending(r => r.ActualGST);
                case "Difference":
                    return sortDir == "asc" ? query.OrderBy(r => r.ExpectedGST - r.ActualGST) : query.OrderByDescending(r => r.ExpectedGST - r.ActualGST);
                case "Status":
                    return sortDir == "asc" ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status);
                case "NameScore":
                    return sortDir == "asc" ? query.OrderBy(r => r.NameScore) : query.OrderByDescending(r => r.NameScore);
                case "AmountScore":
                    return sortDir == "asc" ? query.OrderBy(r => r.AmountScore) : query.OrderByDescending(r => r.AmountScore);
                case "FinalScore":
                    return sortDir == "asc" ? query.OrderBy(r => r.FinalScore) : query.OrderByDescending(r => r.FinalScore);
                case "RERAName":
                default:
                    return sortDir == "asc" ? query.OrderBy(r => r.RERAName) : query.OrderByDescending(r => r.RERAName);
            }
        }

        private static string FormatSheetName(string status)
        {
            switch (status)
            {
                case ReconciliationStatus.Matched: return "Matched Records";
                case ReconciliationStatus.LikelyMatch: return "Likely Match";
                case ReconciliationStatus.PossibleMatch: return "Possible Match";
                case ReconciliationStatus.GstMismatch: return "GST Mismatch";
                case ReconciliationStatus.MissingInGst: return "Missing in GST";
                case ReconciliationStatus.MissingInRera: return "Missing in RERA";
                case ReconciliationStatus.ReraNotGst: return "Missing in GST";
                case ReconciliationStatus.GstNotRera: return "Missing in RERA";
                default: return "Report";
            }
        }
    }
}
