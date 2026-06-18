using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Data
{
    /// <summary>
    /// Entity Framework 6 DbContext for the GST vs RERA Reconciliation database.
    /// Uses the "ReconciliationDb" connection string from Web.config.
    /// All database access goes through EF parameterized queries — no raw SQL concatenation.
    /// </summary>
    public class ReconciliationDbContext : DbContext
    {
        public ReconciliationDbContext()
            : base("name=ReconciliationDb")
        {
        }

        public DbSet<UploadSession> UploadSessions { get; set; }
        public DbSet<RERARecord> RERARecords { get; set; }
        public DbSet<GSTRecord> GSTRecords { get; set; }
        public DbSet<ComparisonResult> ComparisonResults { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ================================================================
            // UploadSession
            // ================================================================
            modelBuilder.Entity<UploadSession>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<UploadSession>()
                .Property(u => u.UploadDate)
                .IsRequired();

            // ================================================================
            // RERARecord
            // ================================================================
            modelBuilder.Entity<RERARecord>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<RERARecord>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(500);

            modelBuilder.Entity<RERARecord>()
                .Property(r => r.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RERARecord>()
                .Property(r => r.OriginalDescription)
                .HasMaxLength(1000);

            modelBuilder.Entity<RERARecord>()
                .HasRequired(r => r.UploadSession)
                .WithMany(s => s.RERARecords)
                .HasForeignKey(r => r.SessionId)
                .WillCascadeOnDelete(true);

            // ================================================================
            // GSTRecord
            // ================================================================
            modelBuilder.Entity<GSTRecord>()
                .HasKey(g => g.Id);

            modelBuilder.Entity<GSTRecord>()
                .Property(g => g.Name)
                .IsRequired()
                .HasMaxLength(500);

            modelBuilder.Entity<GSTRecord>()
                .Property(g => g.GSTAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<GSTRecord>()
                .Property(g => g.OriginalDescription)
                .HasMaxLength(1000);

            modelBuilder.Entity<GSTRecord>()
                .HasRequired(g => g.UploadSession)
                .WithMany(s => s.GSTRecords)
                .HasForeignKey(g => g.SessionId)
                .WillCascadeOnDelete(true);

            // ================================================================
            // ComparisonResult
            // ================================================================
            modelBuilder.Entity<ComparisonResult>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<ComparisonResult>()
                .Property(c => c.RERAName)
                .HasMaxLength(500);

            modelBuilder.Entity<ComparisonResult>()
                .Property(c => c.GSTName)
                .HasMaxLength(500);

            modelBuilder.Entity<ComparisonResult>()
                .Property(c => c.ExpectedGST)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ComparisonResult>()
                .Property(c => c.ActualGST)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ComparisonResult>()
                .Property(c => c.Status)
                .IsRequired()
                .HasMaxLength(30);

            modelBuilder.Entity<ComparisonResult>()
                .HasRequired(c => c.UploadSession)
                .WithMany(s => s.ComparisonResults)
                .HasForeignKey(c => c.SessionId)
                .WillCascadeOnDelete(true);

            // Note: Indexes and the CHECK constraint on Status are defined in the SQL script
            // (SQL\InitialSchema.sql). EF6 Fluent API doesn't support HasDatabaseName() or
            // CHECK constraints. Validation is enforced in application code via ReconciliationStatus.IsValid().
        }
    }
}
