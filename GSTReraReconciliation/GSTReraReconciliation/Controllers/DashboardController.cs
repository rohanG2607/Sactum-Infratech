using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using GSTReraReconciliation.Data.Repositories;
using GSTReraReconciliation.Models;
using GSTReraReconciliation.ViewModels;

namespace GSTReraReconciliation.Controllers
{
    /// <summary>
    /// Controller for the reconciliation dashboard.
    /// Queries ComparisonResults, RERARecords, and GSTRecords via Entity Framework
    /// to build summary statistics and detailed result tables.
    /// Handles both new and legacy status values.
    /// </summary>
    public class DashboardController : Controller
    {
        private readonly IGenericRepository<UploadSession> _sessionRepository;
        private readonly IGenericRepository<ComparisonResult> _resultRepository;
        private readonly IGenericRepository<RERARecord> _reraRepository;
        private readonly IGenericRepository<GSTRecord> _gstRepository;

        public DashboardController(
            IGenericRepository<UploadSession> sessionRepository,
            IGenericRepository<ComparisonResult> resultRepository,
            IGenericRepository<RERARecord> reraRepository,
            IGenericRepository<GSTRecord> gstRepository)
        {
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
            _reraRepository = reraRepository ?? throw new ArgumentNullException(nameof(reraRepository));
            _gstRepository = gstRepository ?? throw new ArgumentNullException(nameof(gstRepository));
        }

        /// <summary>
        /// GET: /Dashboard?sessionId=N
        /// Builds the dashboard with summary cards and results table.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> Index(int? sessionId)
        {
            // ================================================================
            // 1. Query with optional session filter
            // ================================================================
            IQueryable<ComparisonResult> resultQuery = _resultRepository.Find(r => true);
            IQueryable<RERARecord> reraQuery = _reraRepository.Find(r => true);
            IQueryable<GSTRecord> gstQuery = _gstRepository.Find(g => true);

            if (sessionId.HasValue && sessionId.Value > 0)
            {
                resultQuery = resultQuery.Where(r => r.SessionId == sessionId.Value);
                reraQuery = reraQuery.Where(r => r.SessionId == sessionId.Value);
                gstQuery = gstQuery.Where(g => g.SessionId == sessionId.Value);
            }

            // ================================================================
            // 2. Summary counts
            // ================================================================
            int totalRera = await reraQuery.CountAsync();
            int totalGst = await gstQuery.CountAsync();

            var statusCounts = await resultQuery
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // Helper to get count for a status (handles both new and legacy)
            int GetCount(string status)
            {
                return statusCounts.Where(s => s.Status == status).Select(s => s.Count).FirstOrDefault();
            }

            int matchedCount = GetCount(ReconciliationStatus.Matched);
            int likelyMatchCount = GetCount(ReconciliationStatus.LikelyMatch);
            int possibleMatchCount = GetCount(ReconciliationStatus.PossibleMatch);
            int gstMismatchCount = GetCount(ReconciliationStatus.GstMismatch);
            // Combine new + legacy status counts
            int missingInGstCount = GetCount(ReconciliationStatus.MissingInGst) + GetCount(ReconciliationStatus.ReraNotGst);
            int missingInReraCount = GetCount(ReconciliationStatus.MissingInRera) + GetCount(ReconciliationStatus.GstNotRera);
            int totalResults = statusCounts.Sum(s => s.Count);

            // Total difference
            decimal totalDifference = 0m;
            if (totalResults > 0)
            {
                var sums = await resultQuery
                    .Select(r => new { r.ExpectedGST, r.ActualGST })
                    .ToListAsync();
                totalDifference = sums.Sum(r => Math.Abs(r.ExpectedGST - r.ActualGST));
            }

            // ================================================================
            // 3. Detailed results (limited to 200)
            // ================================================================
            var results = await resultQuery
                .OrderByDescending(r =>
                    r.Status == ReconciliationStatus.GstMismatch ? 0 :
                    r.Status == ReconciliationStatus.MissingInGst || r.Status == ReconciliationStatus.ReraNotGst ? 1 :
                    r.Status == ReconciliationStatus.MissingInRera || r.Status == ReconciliationStatus.GstNotRera ? 2 :
                    r.Status == ReconciliationStatus.LikelyMatch ? 3 :
                    r.Status == ReconciliationStatus.PossibleMatch ? 4 : 5)
                .ThenBy(r => r.RERAName)
                .Take(200)
                .ToListAsync();

            // ================================================================
            // 4. Sessions for dropdown
            // ================================================================
            var sessions = (await _sessionRepository.GetAllAsync())
                .OrderByDescending(s => s.UploadDate)
                .Take(20);

            // ================================================================
            // 5. Build ViewModel
            // ================================================================
            var model = new DashboardViewModel
            {
                SelectedSessionId = sessionId,
                TotalRERARecords = totalRera,
                TotalGSTRecords = totalGst,
                MatchedCount = matchedCount,
                LikelyMatchCount = likelyMatchCount,
                PossibleMatchCount = possibleMatchCount,
                GstMismatchCount = gstMismatchCount,
                MissingInGstCount = missingInGstCount,
                MissingInReraCount = missingInReraCount,
                TotalComparisonResults = totalResults,
                TotalDifference = totalDifference,
                Results = results,
                Sessions = sessions
            };

            return View(model);
        }
    }
}
