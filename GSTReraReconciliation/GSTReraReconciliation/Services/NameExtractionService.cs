using System;
using System.Text.RegularExpressions;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Extracts customer names from bank statement description text.
    ///
    /// Handles common Indian bank statement description formats:
    ///   "CLG CR: PRIYANKA SUSHIL RANE"          → PRIYANKA SUSHIL RANE
    ///   "Cr. For NEFT AXOMB00102091866 SHAILENDRA DHONDOO G" → SHAILENDRA DHONDOO G
    ///   "NEFT CR-HDFC0001234-RAJU KORE"         → RAJU KORE
    ///   "RTGS CR-ICIC0001234-COMPANY LTD"       → COMPANY LTD
    ///   "UPI CR-123456@upi-AMIT SHARMA"         → AMIT SHARMA
    ///   "IFT CR: SURESH JADHAV"                 → SURESH JADHAV
    ///   "BY TRANSFER-CR: RAMESH PATIL"          → RAMESH PATIL
    ///
    /// All extracted names are normalized: Trim, ToUpper, collapse extra spaces.
    /// </summary>
    public class NameExtractionService
    {
        // Regex to strip alphanumeric reference codes (e.g., AXOMB00102091866, HDFC0001234)
        // Matches tokens that contain both letters AND digits (like bank ref codes) and are 6+ chars
        private static readonly Regex RefCodePattern =
            new Regex(@"\b(?=[A-Z]*\d)(?=\d*[A-Z])[A-Z0-9]{6,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to collapse multiple whitespace into a single space
        private static readonly Regex MultiSpacePattern =
            new Regex(@"\s{2,}", RegexOptions.Compiled);

        /// <summary>
        /// Extracts a customer name from a bank statement description line.
        /// Returns the normalized name (trimmed, uppercased, extra spaces removed).
        /// Returns null if no meaningful name can be extracted.
        /// </summary>
        public string ExtractName(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            string text = description.Trim();
            string extracted = null;

            // --- Pattern 1: CLG CR: <NAME> ---
            extracted = TryExtractAfterPrefix(text, "CLG CR:");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 2: IFT CR: <NAME> ---
            extracted = TryExtractAfterPrefix(text, "IFT CR:");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 3: BY TRANSFER-CR: <NAME> ---
            extracted = TryExtractAfterPrefix(text, "BY TRANSFER-CR:");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 4: Cr. For NEFT <REFCODE> <NAME> ---
            // Also handles: Cr. For RTGS, Cr. For IMPS
            extracted = TryExtractCrForPattern(text);
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 5: NEFT CR-<BANKCODE>-<NAME> ---
            extracted = TryExtractDashSeparated(text, "NEFT CR");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 6: RTGS CR-<BANKCODE>-<NAME> ---
            extracted = TryExtractDashSeparated(text, "RTGS CR");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 7: UPI CR-<UPIID>-<NAME> ---
            extracted = TryExtractDashSeparated(text, "UPI CR");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 8: IMPS CR-<REFCODE>-<NAME> ---
            extracted = TryExtractDashSeparated(text, "IMPS CR");
            if (extracted != null) return NormalizeName(extracted);

            // --- Pattern 9: General CR: <NAME> ---
            extracted = TryExtractAfterPrefix(text, "CR:");
            if (extracted != null) return NormalizeName(extracted);

            // --- Fallback: Normalize the entire description as the name ---
            // Strip known ref codes and return whatever remains
            string fallback = StripReferenceCodes(text);
            return NormalizeName(fallback);
        }

        /// <summary>
        /// Tries to extract text after a known prefix (case-insensitive).
        /// Returns null if the prefix is not found or nothing follows it.
        /// </summary>
        private static string TryExtractAfterPrefix(string text, string prefix)
        {
            int idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            string after = text.Substring(idx + prefix.Length).Trim();
            return string.IsNullOrWhiteSpace(after) ? null : after;
        }

        /// <summary>
        /// Handles "Cr. For NEFT REFCODE NAME" and similar patterns.
        /// Strips the "Cr. For NEFT/RTGS/IMPS" prefix and the reference code.
        /// </summary>
        private static string TryExtractCrForPattern(string text)
        {
            // Match: Cr. For NEFT|RTGS|IMPS
            string upper = text.ToUpperInvariant();
            int idx = upper.IndexOf("CR. FOR NEFT", StringComparison.Ordinal);
            if (idx < 0) idx = upper.IndexOf("CR. FOR RTGS", StringComparison.Ordinal);
            if (idx < 0) idx = upper.IndexOf("CR. FOR IMPS", StringComparison.Ordinal);
            if (idx < 0) idx = upper.IndexOf("CR.FOR NEFT", StringComparison.Ordinal);
            if (idx < 0) idx = upper.IndexOf("CR.FOR RTGS", StringComparison.Ordinal);
            if (idx < 0) idx = upper.IndexOf("CR.FOR IMPS", StringComparison.Ordinal);
            if (idx < 0) return null;

            // Find the end of "Cr. For NEFT" (12 chars) or "Cr.For NEFT" (11 chars)
            int prefixEnd = text.IndexOf("NEFT", idx, StringComparison.OrdinalIgnoreCase);
            if (prefixEnd < 0) prefixEnd = text.IndexOf("RTGS", idx, StringComparison.OrdinalIgnoreCase);
            if (prefixEnd < 0) prefixEnd = text.IndexOf("IMPS", idx, StringComparison.OrdinalIgnoreCase);
            if (prefixEnd < 0) return null;

            string after = text.Substring(prefixEnd + 4).Trim();
            if (string.IsNullOrWhiteSpace(after)) return null;

            // Strip reference code (first token if it looks like a ref code)
            after = StripReferenceCodes(after);
            return string.IsNullOrWhiteSpace(after) ? null : after;
        }

        /// <summary>
        /// Handles "NEFT CR-BANKCODE-NAME" dash-separated patterns.
        /// Extracts the last segment after the final dash containing the customer name.
        /// </summary>
        private static string TryExtractDashSeparated(string text, string prefix)
        {
            int idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            string after = text.Substring(idx + prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(after)) return null;

            // Remove leading dash
            if (after.StartsWith("-"))
                after = after.Substring(1).Trim();

            // Find the last dash — name is after it
            int lastDash = after.LastIndexOf('-');
            if (lastDash >= 0 && lastDash < after.Length - 1)
            {
                string namePart = after.Substring(lastDash + 1).Trim();
                if (!string.IsNullOrWhiteSpace(namePart))
                    return namePart;
            }

            // No dash found — strip ref codes and return remainder
            after = StripReferenceCodes(after);
            return string.IsNullOrWhiteSpace(after) ? null : after;
        }

        /// <summary>
        /// Removes alphanumeric reference codes (tokens that mix letters+digits, 6+ chars)
        /// from the text, leaving only the customer name.
        /// </summary>
        private static string StripReferenceCodes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return RefCodePattern.Replace(text, "").Trim();
        }

        /// <summary>
        /// Normalizes a name: Trim, ToUpper, collapse multiple spaces into one.
        /// Returns null if the result is empty.
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            name = name.Trim().ToUpperInvariant();
            name = MultiSpacePattern.Replace(name, " ");

            // Strip any trailing/leading punctuation
            name = name.Trim(' ', '-', ':', ',', '.', '/');

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
