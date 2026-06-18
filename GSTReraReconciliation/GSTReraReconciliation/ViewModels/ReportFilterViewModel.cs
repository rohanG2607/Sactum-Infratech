using System.Collections.Generic;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.ViewModels
{
    /// <summary>
    /// ViewModel for the Reports page with filtering, paging, sorting, and search.
    /// </summary>
    public class ReportFilterViewModel
    {
        // ================================================================
        // Filters
        // ================================================================

        /// <summary>Active status filter tab.</summary>
        public string StatusFilter { get; set; }

        /// <summary>Search term to filter by RERA Name or GST Name.</summary>
        public string Search { get; set; }

        /// <summary>Optional session ID filter.</summary>
        public int? SessionId { get; set; }

        // ================================================================
        // Sorting
        // ================================================================

        /// <summary>Column to sort by.</summary>
        public string SortBy { get; set; }

        /// <summary>Sort direction: asc or desc</summary>
        public string SortDir { get; set; }

        /// <summary>Returns the opposite sort direction.</summary>
        public string ToggleSortDir
        {
            get { return SortDir == "asc" ? "desc" : "asc"; }
        }

        /// <summary>Returns the sort icon CSS class for a given column.</summary>
        public string SortIcon(string column)
        {
            if (SortBy != column) return "bi-chevron-expand";
            return SortDir == "asc" ? "bi-sort-up" : "bi-sort-down";
        }

        /// <summary>Returns the sort direction to use when clicking a column header.</summary>
        public string SortDirFor(string column)
        {
            if (SortBy == column) return ToggleSortDir;
            return "asc";
        }

        // ================================================================
        // Paging
        // ================================================================

        /// <summary>Current page number (1-based).</summary>
        public int Page { get; set; }

        /// <summary>Number of records per page.</summary>
        public int PageSize { get; set; }

        /// <summary>Total number of records matching filters.</summary>
        public int TotalRecords { get; set; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages
        {
            get
            {
                if (TotalRecords <= 0 || PageSize <= 0) return 1;
                return (int)System.Math.Ceiling((double)TotalRecords / PageSize);
            }
        }

        /// <summary>Whether there is a previous page.</summary>
        public bool HasPreviousPage
        {
            get { return Page > 1; }
        }

        /// <summary>Whether there is a next page.</summary>
        public bool HasNextPage
        {
            get { return Page < TotalPages; }
        }

        /// <summary>Start record number for display.</summary>
        public int StartRecord
        {
            get { return TotalRecords == 0 ? 0 : (Page - 1) * PageSize + 1; }
        }

        /// <summary>End record number for display.</summary>
        public int EndRecord
        {
            get { return System.Math.Min(Page * PageSize, TotalRecords); }
        }

        // ================================================================
        // Results
        // ================================================================

        /// <summary>The paged, filtered, sorted results to display.</summary>
        public IEnumerable<ComparisonResult> Results { get; set; }

        // ================================================================
        // Tab Counts (for badge display on each tab)
        // ================================================================

        public int AllCount { get; set; }
        public int MatchedCount { get; set; }
        public int LikelyMatchCount { get; set; }
        public int PossibleMatchCount { get; set; }
        public int GstMismatchCount { get; set; }
        public int MissingInGstCount { get; set; }
        public int MissingInReraCount { get; set; }

        /// <summary>Available sessions for the dropdown filter.</summary>
        public IEnumerable<UploadSession> Sessions { get; set; }

        public ReportFilterViewModel()
        {
            Page = 1;
            PageSize = 25;
            SortBy = "RERAName";
            SortDir = "asc";
            Results = new List<ComparisonResult>();
            Sessions = new List<UploadSession>();
        }
    }
}
