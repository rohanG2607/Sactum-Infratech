using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTReraReconciliation.Models
{
    /// <summary>
    /// Reconciliation status values for comparison results.
    /// Stored as string in the database for readability.
    /// </summary>
    public static class ReconciliationStatus
    {
        /// <summary>RERA and GST records match by name and amount.</summary>
        public const string Matched = "MATCHED";

        /// <summary>Name matched with high confidence + amount within tolerance.</summary>
        public const string LikelyMatch = "LIKELY_MATCH";

        /// <summary>Fuzzy match — names are similar but not exact.</summary>
        public const string PossibleMatch = "POSSIBLE_MATCH";

        /// <summary>Both records exist but GST amounts do not match.</summary>
        public const string GstMismatch = "GST_MISMATCH";

        /// <summary>RERA record exists but no corresponding GST record found.</summary>
        public const string MissingInGst = "MISSING_IN_GST";

        /// <summary>GST record exists but no corresponding RERA record found.</summary>
        public const string MissingInRera = "MISSING_IN_RERA";

        // --- Legacy status constants (for backward compatibility with existing DB data) ---
        public const string ReraNotGst = "RERA_NOT_GST";
        public const string GstNotRera = "GST_NOT_RERA";

        /// <summary>
        /// All valid status values for validation (includes both new and legacy).
        /// </summary>
        public static readonly string[] AllStatuses = new[]
        {
            Matched,
            LikelyMatch,
            PossibleMatch,
            GstMismatch,
            MissingInGst,
            MissingInRera,
            ReraNotGst,   // legacy
            GstNotRera    // legacy
        };

        /// <summary>
        /// Returns true if the given value is a valid reconciliation status.
        /// </summary>
        public static bool IsValid(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            foreach (var s in AllStatuses)
            {
                if (s == status) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a human-readable display label for a status constant.
        /// Handles both new and legacy status values.
        /// </summary>
        public static string DisplayLabel(string status)
        {
            switch (status)
            {
                case Matched: return "Matched";
                case LikelyMatch: return "Likely Match";
                case PossibleMatch: return "Possible Match";
                case GstMismatch: return "GST Mismatch";
                case MissingInGst: return "Missing in GST";
                case MissingInRera: return "Missing in RERA";
                case ReraNotGst: return "Missing in GST";
                case GstNotRera: return "Missing in RERA";
                default: return status;
            }
        }
    }

    /// <summary>
    /// Represents the result of comparing a RERA record against a GST record.
    /// RERAName/GSTName are nullable to represent unmatched records from either side.
    /// Status is stored as a string constant from <see cref="ReconciliationStatus"/>.
    /// Includes match quality scores for advanced reconciliation reporting.
    /// </summary>
    [Table("ComparisonResults")]
    public class ComparisonResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Session")]
        public int SessionId { get; set; }

        [ForeignKey("SessionId")]
        public virtual UploadSession UploadSession { get; set; }

        /// <summary>
        /// Name from the RERA bank statement. Null when Status is MISSING_IN_RERA.
        /// </summary>
        [StringLength(500)]
        [Display(Name = "RERA Name")]
        public string RERAName { get; set; }

        /// <summary>
        /// Name from the GST return. Null when Status is MISSING_IN_GST.
        /// </summary>
        [StringLength(500)]
        [Display(Name = "GST Name")]
        public string GSTName { get; set; }

        /// <summary>
        /// The GST amount that was expected (derived from the RERA record).
        /// </summary>
        [Required]
        [Display(Name = "Expected GST")]
        public decimal ExpectedGST { get; set; }

        /// <summary>
        /// The GST amount that was actually found in the GST record.
        /// </summary>
        [Required]
        [Display(Name = "Actual GST")]
        public decimal ActualGST { get; set; }

        /// <summary>
        /// Reconciliation status. Use <see cref="ReconciliationStatus"/> constants.
        /// </summary>
        [Required]
        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; }

        /// <summary>
        /// Name similarity score from FuzzySharp (0–100).
        /// 100 = exact match, 0 = no similarity.
        /// </summary>
        [Display(Name = "Name Score")]
        public int NameScore { get; set; }

        /// <summary>
        /// Amount closeness score (0–100).
        /// 100 = exact amount match, decreases as difference grows.
        /// </summary>
        [Display(Name = "Amount Score")]
        public int AmountScore { get; set; }

        /// <summary>
        /// Weighted final match score: (NameScore × 0.6) + (AmountScore × 0.4).
        /// </summary>
        [Display(Name = "Final Score")]
        public int FinalScore { get; set; }

        /// <summary>
        /// Computed difference between expected and actual GST (not mapped to DB).
        /// </summary>
        [NotMapped]
        public decimal Difference
        {
            get { return ExpectedGST - ActualGST; }
        }
    }
}
