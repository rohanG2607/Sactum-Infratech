using System;
using System.Collections.Generic;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Filters out non-customer transactions from bank statement rows.
    ///
    /// Bank statements contain many internal transactions (standing instructions,
    /// reversals, loan repayments, charges, etc.) that should not be included in
    /// the reconciliation. This service checks the description text against a
    /// configurable blocklist of keywords.
    /// </summary>
    public class TransactionFilterService
    {
        /// <summary>
        /// Keywords that indicate a non-customer transaction.
        /// If any of these appear in the description (case-insensitive), the row is skipped.
        /// </summary>
        private static readonly List<string> BlockedKeywords = new List<string>
        {
            "SI:",                  // Standing Instruction
            "STANDING INSTRUCTION",
            "REVERSAL",
            "TERM LOAN",
            "BALANCE",
            "CHARGES",
            "INTERNAL",
            "INTEREST",
            "TDS",
            "SERVICE TAX",
            "SWEEP",
            "GST ON",             // GST charges on bank services
            "LOAN EMI",
            "LOAN REPAY",
            "CLOSURE",
            "PENALTY",
            "COMMISSION",
            "INSURANCE",
            "DEBIT CARD",
            "ATM",
            "CASH DEPOSIT",
            "CASH WITHDRAWAL"
        };

        /// <summary>
        /// Returns true if the transaction should be INCLUDED (is a valid customer transaction).
        /// Returns false if the transaction should be SKIPPED (matches a blocked keyword).
        /// </summary>
        /// <param name="description">The raw description text from the bank statement.</param>
        public bool IsCustomerTransaction(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;

            string upper = description.ToUpperInvariant();

            foreach (var keyword in BlockedKeywords)
            {
                if (upper.Contains(keyword.ToUpperInvariant()))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the blocked keyword that matched, or null if no match.
        /// Useful for logging/debugging which filter blocked a row.
        /// </summary>
        /// <param name="description">The raw description text.</param>
        public string GetBlockedReason(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "Empty description";

            string upper = description.ToUpperInvariant();

            foreach (var keyword in BlockedKeywords)
            {
                if (upper.Contains(keyword.ToUpperInvariant()))
                {
                    return keyword;
                }
            }

            return null;
        }
    }
}
