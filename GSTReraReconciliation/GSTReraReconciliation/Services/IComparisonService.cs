using System.Collections.Generic;
using System.Threading.Tasks;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Service interface for comparing GST records against RERA bank statement records.
    /// </summary>
    public interface IComparisonService
    {
        /// <summary>
        /// Runs the reconciliation comparison for all RERA and GST records within a session.
        /// Produces ComparisonResult rows with status: MATCHED, RERA_NOT_GST, GST_NOT_RERA, GST_MISMATCH, POSSIBLE_MATCH.
        /// </summary>
        /// <param name="sessionId">The upload session ID containing both RERA and GST records.</param>
        /// <returns>Collection of comparison results.</returns>
        Task<IEnumerable<ComparisonResult>> RunComparisonAsync(int sessionId);
    }
}
