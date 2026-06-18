using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTReraReconciliation.Models
{
    /// <summary>
    /// Represents a single row from a RERA bank statement.
    /// </summary>
    [Table("RERARecords")]
    public class RERARecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Session")]
        public int SessionId { get; set; }

        [ForeignKey("SessionId")]
        public virtual UploadSession UploadSession { get; set; }

        [Required]
        [StringLength(500)]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// The raw description text from the bank statement before name extraction.
        /// </summary>
        [StringLength(1000)]
        [Display(Name = "Original Description")]
        public string OriginalDescription { get; set; }
    }
}
