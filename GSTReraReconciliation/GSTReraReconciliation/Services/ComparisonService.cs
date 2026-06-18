using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using FuzzySharp;
using GSTReraReconciliation.Data.Repositories;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Multi-stage matching engine for comparing GST records against RERA bank statement records.
    ///
    /// Matching Algorithm (3 stages):
    ///
    ///   Stage 1 — Exact Name Match:
    ///     Dictionary O(1) lookup by normalized name.
    ///     MATCHED        — Name exact + amount match  (Score: 100/100/100)
    ///     GST_MISMATCH   — Name exact + amount differs (Score: 100/calc/calc)
    ///
    ///   Stage 2 — Fuzzy Name Match:
    ///     FuzzySharp WeightedRatio on unmatched records.
    ///     LIKELY_MATCH   — Score ≥ 90 AND amount within 10% tolerance
    ///     POSSIBLE_MATCH — Score ≥ 80
    ///
    ///   Stage 3 — Amount-Based Match:
    ///     For still-unmatched RERA records, search by amount proximity.
    ///     LIKELY_MATCH   — ExpectedGST matches ActualGST within ₹1 tolerance
    ///
    ///   Unmatched:
    ///     MISSING_IN_GST — RERA record with no GST counterpart
    ///     MISSING_IN_RERA — GST record with no RERA counterpart
    ///
    /// Scoring:
    ///   NameScore   = 0–100 (FuzzySharp similarity)
    ///   AmountScore = max(0, 100 - (|expected-actual| / max(expected,1) × 100))
    ///   FinalScore  = (int)(NameScore × 0.6 + AmountScore × 0.4)
    /// </summary>
    public class ComparisonService : IComparisonService
    {
        private readonly IGenericRepository<GSTRecord> _gstRepository;
        private readonly IGenericRepository<RERARecord> _reraRepository;
        private readonly IGenericRepository<ComparisonResult> _resultRepository;

        /// <summary>GST rate applied to RERA Amount to calculate Expected GST. 5% = 0.05</summary>
        private const decimal GSTRate = 0.05m;

        /// <summary>Minimum FuzzySharp score (0–100) to consider a possible match.</summary>
        private const int FuzzyPossibleThreshold = 80;

        /// <summary>Minimum FuzzySharp score (0–100) for a likely match.</summary>
        private const int FuzzyLikelyThreshold = 90;

        /// <summary>Amount tolerance percentage for likely match (10% = 0.10).</summary>
        private const decimal AmountTolerancePercent = 0.10m;

        /// <summary>Absolute amount tolerance for amount-based matching (₹1).</summary>
        private const decimal AmountAbsoluteTolerance = 1.0m;

        public ComparisonService(
            IGenericRepository<GSTRecord> gstRepository,
            IGenericRepository<RERARecord> reraRepository,
            IGenericRepository<ComparisonResult> resultRepository)
        {
            _gstRepository = gstRepository ?? throw new ArgumentNullException(nameof(gstRepository));
            _reraRepository = reraRepository ?? throw new ArgumentNullException(nameof(reraRepository));
            _resultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ComparisonResult>> RunComparisonAsync(int sessionId)
        {
            // ================================================================
            // Step 1: Load all records for this session
            // ================================================================
            var reraRecords = await _reraRepository
                .Find(r => r.SessionId == sessionId)
                .ToListAsync();

            var gstRecords = await _gstRepository
                .Find(g => g.SessionId == sessionId)
                .ToListAsync();

            if (reraRecords.Count == 0 && gstRecords.Count == 0)
            {
                throw new InvalidOperationException(
                    "No RERA or GST records found for session #" + sessionId + ". " +
                    "Please upload both files before comparing.");
            }

            // ================================================================
            // Step 2: Build Dictionary for O(1) GST lookups by normalized name
            // ================================================================
            var gstLookup = new Dictionary<string, List<GSTRecord>>(StringComparer.Ordinal);

            foreach (var gst in gstRecords)
            {
                string normalizedName = NormalizeName(gst.Name);
                if (string.IsNullOrEmpty(normalizedName)) continue;

                if (!gstLookup.ContainsKey(normalizedName))
                {
                    gstLookup[normalizedName] = new List<GSTRecord>();
                }
                gstLookup[normalizedName].Add(gst);
            }

            var matchedGstIds = new HashSet<int>();
            var results = new List<ComparisonResult>();
            var unmatchedReraRecords = new List<RERARecord>();

            // ================================================================
            // Stage 1: Exact Name Match
            // ================================================================
            foreach (var rera in reraRecords)
            {
                string reraNameNormalized = NormalizeName(rera.Name);
                decimal expectedGst = CalculateExpectedGST(rera.Amount);

                if (!string.IsNullOrEmpty(reraNameNormalized) &&
                    gstLookup.TryGetValue(reraNameNormalized, out List<GSTRecord> matchingGstList))
                {
                    var gstMatch = matchingGstList.FirstOrDefault(g => !matchedGstIds.Contains(g.Id));

                    if (gstMatch != null)
                    {
                        matchedGstIds.Add(gstMatch.Id);
                        int amountScore = CalculateAmountScore(expectedGst, gstMatch.GSTAmount);

                        if (expectedGst == gstMatch.GSTAmount)
                        {
                            // MATCHED — exact name + exact amount
                            results.Add(CreateResult(sessionId, rera.Name, gstMatch.Name,
                                expectedGst, gstMatch.GSTAmount, ReconciliationStatus.Matched,
                                nameScore: 100, amountScore: 100, finalScore: 100));
                        }
                        else
                        {
                            // GST_MISMATCH — exact name, different amount
                            int finalScore = CalculateFinalScore(100, amountScore);
                            results.Add(CreateResult(sessionId, rera.Name, gstMatch.Name,
                                expectedGst, gstMatch.GSTAmount, ReconciliationStatus.GstMismatch,
                                nameScore: 100, amountScore: amountScore, finalScore: finalScore));
                        }
                        continue;
                    }
                }

                unmatchedReraRecords.Add(rera);
            }

            // ================================================================
            // Stage 2: Fuzzy Name Match
            // ================================================================
            var unmatchedGstRecords = gstRecords
                .Where(g => !matchedGstIds.Contains(g.Id))
                .ToList();

            var stillUnmatchedRera = new List<RERARecord>();

            foreach (var rera in unmatchedReraRecords)
            {
                string reraNameNormalized = NormalizeName(rera.Name);
                decimal expectedGst = CalculateExpectedGST(rera.Amount);

                if (string.IsNullOrEmpty(reraNameNormalized) || unmatchedGstRecords.Count == 0)
                {
                    stillUnmatchedRera.Add(rera);
                    continue;
                }

                var bestMatch = FindBestFuzzyMatch(reraNameNormalized, unmatchedGstRecords, matchedGstIds);

                if (bestMatch.HasValue)
                {
                    var gstMatch = bestMatch.Value.Record;
                    int nameScore = bestMatch.Value.Score;
                    int amountScore = CalculateAmountScore(expectedGst, gstMatch.GSTAmount);
                    int finalScore = CalculateFinalScore(nameScore, amountScore);

                    // Determine if LIKELY_MATCH or POSSIBLE_MATCH
                    bool amountWithinTolerance = IsAmountWithinTolerance(expectedGst, gstMatch.GSTAmount);
                    string status;

                    if (nameScore >= FuzzyLikelyThreshold && amountWithinTolerance)
                    {
                        status = ReconciliationStatus.LikelyMatch;
                    }
                    else
                    {
                        status = ReconciliationStatus.PossibleMatch;
                    }

                    matchedGstIds.Add(gstMatch.Id);
                    unmatchedGstRecords.Remove(gstMatch);

                    results.Add(CreateResult(sessionId, rera.Name, gstMatch.Name,
                        expectedGst, gstMatch.GSTAmount, status,
                        nameScore: nameScore, amountScore: amountScore, finalScore: finalScore));
                }
                else
                {
                    stillUnmatchedRera.Add(rera);
                }
            }

            // ================================================================
            // Stage 3: Amount-Based Match (for still-unmatched RERA records)
            // ================================================================
            foreach (var rera in stillUnmatchedRera)
            {
                decimal expectedGst = CalculateExpectedGST(rera.Amount);
                string reraNameNormalized = NormalizeName(rera.Name);

                // Search unmatched GST records for an amount match
                GSTRecord amountMatch = null;
                int bestNameScore = 0;

                foreach (var gst in unmatchedGstRecords)
                {
                    if (matchedGstIds.Contains(gst.Id)) continue;

                    // Check if amount matches within ₹1 tolerance
                    if (Math.Abs(expectedGst - gst.GSTAmount) <= AmountAbsoluteTolerance)
                    {
                        // Calculate name score for this candidate
                        string gstNameNormalized = NormalizeName(gst.Name);
                        int nameScore = 0;
                        if (!string.IsNullOrEmpty(reraNameNormalized) && !string.IsNullOrEmpty(gstNameNormalized))
                        {
                            nameScore = Fuzz.WeightedRatio(reraNameNormalized, gstNameNormalized);
                        }

                        if (amountMatch == null || nameScore > bestNameScore)
                        {
                            amountMatch = gst;
                            bestNameScore = nameScore;
                        }
                    }
                }

                if (amountMatch != null)
                {
                    matchedGstIds.Add(amountMatch.Id);
                    unmatchedGstRecords.Remove(amountMatch);

                    int amountScore = CalculateAmountScore(expectedGst, amountMatch.GSTAmount);
                    int finalScore = CalculateFinalScore(bestNameScore, amountScore);

                    results.Add(CreateResult(sessionId, rera.Name, amountMatch.Name,
                        expectedGst, amountMatch.GSTAmount, ReconciliationStatus.LikelyMatch,
                        nameScore: bestNameScore, amountScore: amountScore, finalScore: finalScore));
                }
                else
                {
                    // MISSING_IN_GST — no match found
                    results.Add(CreateResult(sessionId, rera.Name, null,
                        expectedGst, 0m, ReconciliationStatus.MissingInGst,
                        nameScore: 0, amountScore: 0, finalScore: 0));
                }
            }

            // ================================================================
            // Step 4: Add remaining unmatched GST records
            // ================================================================
            foreach (var gst in gstRecords)
            {
                if (!matchedGstIds.Contains(gst.Id))
                {
                    results.Add(CreateResult(sessionId, null, gst.Name,
                        0m, gst.GSTAmount, ReconciliationStatus.MissingInRera,
                        nameScore: 0, amountScore: 0, finalScore: 0));
                }
            }

            // ================================================================
            // Step 5: Persist all results
            // ================================================================
            _resultRepository.AddRange(results);
            await _resultRepository.SaveChangesAsync();

            return results;
        }

        // ================================================================
        // Scoring Helpers
        // ================================================================

        /// <summary>Normalizes a name: Trim + ToUpper.</summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return name.Trim().ToUpperInvariant();
        }

        /// <summary>Calculates Expected GST as Amount × 5%, rounded to 2 decimal places.</summary>
        private static decimal CalculateExpectedGST(decimal amount)
        {
            return Math.Round(amount * GSTRate, 2, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Calculates amount closeness score (0–100).
        /// 100 = exact match, decreases as difference grows relative to expected amount.
        /// </summary>
        private static int CalculateAmountScore(decimal expected, decimal actual)
        {
            if (expected == actual) return 100;
            decimal denominator = Math.Max(Math.Abs(expected), 1m);
            decimal percentDiff = Math.Abs(expected - actual) / denominator * 100m;
            int score = Math.Max(0, (int)(100m - percentDiff));
            return score;
        }

        /// <summary>
        /// Calculates weighted final score: (NameScore × 0.6) + (AmountScore × 0.4).
        /// </summary>
        private static int CalculateFinalScore(int nameScore, int amountScore)
        {
            return (int)(nameScore * 0.6 + amountScore * 0.4);
        }

        /// <summary>
        /// Returns true if the actual amount is within the tolerance of expected.
        /// Tolerance = 10% of expected amount.
        /// </summary>
        private static bool IsAmountWithinTolerance(decimal expected, decimal actual)
        {
            if (expected == 0m) return actual == 0m;
            decimal tolerance = Math.Abs(expected) * AmountTolerancePercent;
            return Math.Abs(expected - actual) <= tolerance;
        }

        /// <summary>
        /// Finds the best fuzzy match for a RERA name among unmatched GST records.
        /// Returns null if no match exceeds the threshold (80).
        /// </summary>
        private static FuzzyMatchResult? FindBestFuzzyMatch(
            string reraNameNormalized,
            List<GSTRecord> unmatchedGstRecords,
            HashSet<int> matchedGstIds)
        {
            int bestScore = 0;
            GSTRecord bestRecord = null;

            foreach (var gst in unmatchedGstRecords)
            {
                if (matchedGstIds.Contains(gst.Id)) continue;

                string gstNameNormalized = NormalizeName(gst.Name);
                if (string.IsNullOrEmpty(gstNameNormalized)) continue;

                int score = Fuzz.WeightedRatio(reraNameNormalized, gstNameNormalized);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRecord = gst;
                }
            }

            if (bestScore >= FuzzyPossibleThreshold && bestRecord != null)
            {
                return new FuzzyMatchResult { Record = bestRecord, Score = bestScore };
            }

            return null;
        }

        /// <summary>Creates a ComparisonResult with all fields including scores.</summary>
        private static ComparisonResult CreateResult(
            int sessionId, string reraName, string gstName,
            decimal expectedGst, decimal actualGst, string status,
            int nameScore, int amountScore, int finalScore)
        {
            return new ComparisonResult
            {
                SessionId = sessionId,
                RERAName = reraName,
                GSTName = gstName,
                ExpectedGST = expectedGst,
                ActualGST = actualGst,
                Status = status,
                NameScore = nameScore,
                AmountScore = amountScore,
                FinalScore = finalScore
            };
        }

        /// <summary>Internal struct to hold a fuzzy match result with its score.</summary>
        private struct FuzzyMatchResult
        {
            public GSTRecord Record;
            public int Score;
        }
    }
}
