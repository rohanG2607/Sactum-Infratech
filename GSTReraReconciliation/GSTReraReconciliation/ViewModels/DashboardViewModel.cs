using System.Collections.Generic;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.ViewModels
{
    /// <summary>
    /// ViewModel for the reconciliation dashboard.
    /// Provides summary counts for all reconciliation statuses and detailed results.
    /// </summary>
    public class DashboardViewModel
    {
        // --- Record Counts ---

        /// <summary>Total RERA records across all sessions (or filtered session).</summary>
        public int TotalRERARecords { get; set; }

        /// <summary>Total GST records across all sessions (or filtered session).</summary>
        public int TotalGSTRecords { get; set; }

        // --- Reconciliation Status Counts ---

        /// <summary>Count of records where RERA name matched GST name AND Expected GST == Actual GST.</summary>
        public int MatchedCount { get; set; }

        /// <summary>Count of high-confidence matches (fuzzy name ≥90 + amount within tolerance, or amount-based match).</summary>
        public int LikelyMatchCount { get; set; }

        /// <summary>Count of records matched via fuzzy name matching (score ≥ 80).</summary>
        public int PossibleMatchCount { get; set; }

        /// <summary>Count of records where name matched but Expected GST != Actual GST.</summary>
        public int GstMismatchCount { get; set; }

        /// <summary>Count of RERA records with no corresponding GST record.</summary>
        public int MissingInGstCount { get; set; }

        /// <summary>Count of GST records with no corresponding RERA record.</summary>
        public int MissingInReraCount { get; set; }

        /// <summary>Total number of comparison result rows.</summary>
        public int TotalComparisonResults { get; set; }

        // --- Computed Properties ---

        /// <summary>Sum of absolute differences |ExpectedGST - ActualGST| across all results.</summary>
        public decimal TotalDifference { get; set; }

        /// <summary>Match percentage ((Matched + LikelyMatch) / TotalComparisonResults × 100).</summary>
        public decimal MatchPercentage
        {
            get
            {
                if (TotalComparisonResults == 0) return 0;
                return System.Math.Round((decimal)(MatchedCount + LikelyMatchCount) / TotalComparisonResults * 100, 1);
            }
        }

        /// <summary>Discrepancy percentage (Mismatches + Missing / TotalComparisonResults × 100).</summary>
        public decimal DiscrepancyPercentage
        {
            get
            {
                if (TotalComparisonResults == 0) return 0;
                int discrepancies = GstMismatchCount + MissingInGstCount + MissingInReraCount;
                return System.Math.Round((decimal)discrepancies / TotalComparisonResults * 100, 1);
            }
        }

        // --- Detailed Results ---

        /// <summary>All comparison results for the table display.</summary>
        public IEnumerable<ComparisonResult> Results { get; set; }

        /// <summary>Available sessions for the session filter dropdown.</summary>
        public IEnumerable<UploadSession> Sessions { get; set; }

        /// <summary>Currently selected session ID (null = all sessions).</summary>
        public int? SelectedSessionId { get; set; }

        public DashboardViewModel()
        {
            Results = new List<ComparisonResult>();
            Sessions = new List<UploadSession>();
        }
    }
}
