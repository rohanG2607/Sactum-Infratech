using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTReraReconciliation.Models
{
    /// <summary>
    /// Represents a single upload session.
    /// Each session groups a set of RERA records, GST records, and comparison results.
    /// </summary>
    [Table("UploadSessions")]
    public class UploadSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; }

        // --- Navigation Properties ---

        public virtual ICollection<RERARecord> RERARecords { get; set; }
        public virtual ICollection<GSTRecord> GSTRecords { get; set; }
        public virtual ICollection<ComparisonResult> ComparisonResults { get; set; }

        public UploadSession()
        {
            UploadDate = DateTime.UtcNow;
            RERARecords = new HashSet<RERARecord>();
            GSTRecords = new HashSet<GSTRecord>();
            ComparisonResults = new HashSet<ComparisonResult>();
        }
    }
}
