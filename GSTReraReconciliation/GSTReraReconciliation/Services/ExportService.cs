using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using GSTReraReconciliation.Models;

namespace GSTReraReconciliation.Services
{
    /// <summary>
    /// Service for exporting comparison results to Excel using EPPlus 4.5.3.3.
    /// Generates professional, detailed multi-sheet workbooks with:
    ///   - Summary sheet with aggregate statistics
    ///   - All Records sheet with every record and full detail columns
    ///   - Status-specific sheets (Matched, Likely Match, Possible Match, etc.)
    ///   - Score columns (Name Score, Amount Score, Final Score)
    ///   - Color-coded rows, currency formatting, auto-fit columns
    /// </summary>
    public class ExportService : IExportService
    {
        // ================================================================
        // Color Palette
        // ================================================================
        private static readonly Color HeaderBgColor = Color.FromArgb(26, 26, 46);
        private static readonly Color HeaderFontColor = Color.White;
        private static readonly Color SummaryHeaderBg = Color.FromArgb(102, 126, 234);
        private static readonly Color MatchedRowColor = Color.FromArgb(212, 237, 218);
        private static readonly Color LikelyMatchRowColor = Color.FromArgb(199, 230, 255);
        private static readonly Color MismatchRowColor = Color.FromArgb(255, 243, 205);
        private static readonly Color MissingInGstRowColor = Color.FromArgb(248, 215, 218);
        private static readonly Color MissingInReraRowColor = Color.FromArgb(226, 227, 229);
        private static readonly Color PossibleMatchRowColor = Color.FromArgb(209, 236, 244);
        private static readonly Color AlternateRowColor = Color.FromArgb(249, 250, 252);

        // Status definitions for organized export
        private static readonly StatusDefinition[] StatusDefinitions = new[]
        {
            new StatusDefinition("Matched Records",   new[] { ReconciliationStatus.Matched },                                        MatchedRowColor,      Color.FromArgb(28, 128, 65)),
            new StatusDefinition("Likely Match",       new[] { ReconciliationStatus.LikelyMatch },                                    LikelyMatchRowColor,  Color.FromArgb(0, 86, 179)),
            new StatusDefinition("Possible Match",     new[] { ReconciliationStatus.PossibleMatch },                                  PossibleMatchRowColor, Color.FromArgb(56, 61, 65)),
            new StatusDefinition("GST Mismatch",       new[] { ReconciliationStatus.GstMismatch },                                    MismatchRowColor,     Color.FromArgb(133, 100, 4)),
            new StatusDefinition("Missing in GST",     new[] { ReconciliationStatus.MissingInGst, ReconciliationStatus.ReraNotGst },   MissingInGstRowColor, Color.FromArgb(136, 28, 36)),
            new StatusDefinition("Missing in RERA",    new[] { ReconciliationStatus.MissingInRera, ReconciliationStatus.GstNotRera },  MissingInReraRowColor, Color.FromArgb(27, 30, 33)),
        };

        // ================================================================
        // Export All — Multi-Sheet Workbook (detailed)
        // ================================================================

        /// <inheritdoc />
        public Task<byte[]> ExportAllToExcelAsync(IEnumerable<ComparisonResult> results)
        {
            var resultList = results != null ? results.ToList() : new List<ComparisonResult>();

            using (var package = new ExcelPackage())
            {
                // --- Sheet 1: Summary Dashboard ---
                CreateSummarySheet(package, resultList);

                // --- Sheet 2: All Records (complete detail) ---
                CreateDetailedDataSheet(package, "All Records", resultList, showStatusColumn: true);

                // --- Per-status detail sheets ---
                foreach (var def in StatusDefinitions)
                {
                    var filtered = resultList.Where(r => def.StatusValues.Contains(r.Status)).ToList();
                    if (filtered.Count > 0)
                    {
                        CreateDetailedDataSheet(package, def.SheetName, filtered, showStatusColumn: false);
                    }
                }

                return Task.FromResult(package.GetAsByteArray());
            }
        }

        // ================================================================
        // Export Filtered — Single Sheet (detailed)
        // ================================================================

        /// <inheritdoc />
        public Task<byte[]> ExportToExcelAsync(IEnumerable<ComparisonResult> results, string sheetName = "Report")
        {
            var resultList = results != null ? results.ToList() : new List<ComparisonResult>();

            using (var package = new ExcelPackage())
            {
                CreateDetailedDataSheet(package, sheetName, resultList, showStatusColumn: true);
                return Task.FromResult(package.GetAsByteArray());
            }
        }

        // ================================================================
        // Summary Sheet — Dashboard Overview
        // ================================================================

        private void CreateSummarySheet(ExcelPackage package, List<ComparisonResult> results)
        {
            var ws = package.Workbook.Worksheets.Add("Summary");

            // --- Title Block ---
            ws.Cells["A1"].Value = "GST vs RERA Reconciliation Report";
            ws.Cells["A1:F1"].Merge = true;
            ws.Cells["A1"].Style.Font.Size = 18;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Color.SetColor(HeaderBgColor);

            ws.Cells["A2"].Value = "Generated: " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
            ws.Cells["A2:F2"].Merge = true;
            ws.Cells["A2"].Style.Font.Italic = true;
            ws.Cells["A2"].Style.Font.Size = 10;
            ws.Cells["A2"].Style.Font.Color.SetColor(Color.Gray);

            ws.Cells["A3"].Value = "Total Records: " + results.Count;
            ws.Cells["A3:F3"].Merge = true;
            ws.Cells["A3"].Style.Font.Size = 10;
            ws.Cells["A3"].Style.Font.Color.SetColor(Color.DimGray);

            // --- Status Breakdown Table ---
            int row = 5;
            string[] summaryHeaders = { "Status Category", "Count", "%", "Total Expected GST", "Total Actual GST", "Total Difference" };
            for (int i = 0; i < summaryHeaders.Length; i++)
            {
                ws.Cells[row, i + 1].Value = summaryHeaders[i];
            }
            using (var headerRange = ws.Cells[row, 1, row, summaryHeaders.Length])
            {
                StyleHeader(headerRange, SummaryHeaderBg);
            }

            foreach (var def in StatusDefinitions)
            {
                row++;
                var groupResults = results.Where(r => def.StatusValues.Contains(r.Status)).ToList();
                int count = groupResults.Count;
                decimal expectedSum = groupResults.Sum(r => r.ExpectedGST);
                decimal actualSum = groupResults.Sum(r => r.ActualGST);
                decimal diffSum = groupResults.Sum(r => Math.Abs(r.ExpectedGST - r.ActualGST));
                double pct = results.Count > 0 ? (double)count / results.Count * 100 : 0;

                ws.Cells[row, 1].Value = def.SheetName;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 2].Value = count;
                ws.Cells[row, 3].Value = Math.Round(pct, 1);
                ws.Cells[row, 4].Value = expectedSum;
                ws.Cells[row, 5].Value = actualSum;
                ws.Cells[row, 6].Value = diffSum;

                using (var rowRange = ws.Cells[row, 1, row, summaryHeaders.Length])
                {
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(def.RowColor);
                    AddThinBorder(rowRange);
                }
            }

            // Totals row
            row++;
            ws.Cells[row, 1].Value = "TOTAL";
            ws.Cells[row, 2].Value = results.Count;
            ws.Cells[row, 3].Value = 100.0;
            ws.Cells[row, 4].Value = results.Sum(r => r.ExpectedGST);
            ws.Cells[row, 5].Value = results.Sum(r => r.ActualGST);
            ws.Cells[row, 6].Value = results.Sum(r => Math.Abs(r.ExpectedGST - r.ActualGST));

            using (var totalRange = ws.Cells[row, 1, row, summaryHeaders.Length])
            {
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                totalRange.Style.Fill.BackgroundColor.SetColor(HeaderBgColor);
                totalRange.Style.Font.Color.SetColor(Color.White);
                AddThinBorder(totalRange);
            }

            // Format
            ws.Cells[6, 3, row, 3].Style.Numberformat.Format = "0.0\"%\"";
            ws.Cells[6, 4, row, 6].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[5, 2, row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[5, 4, row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            // --- Key Metrics Section ---
            row += 2;
            ws.Cells[row, 1].Value = "Key Metrics";
            ws.Cells[row, 1].Style.Font.Size = 13;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Color.SetColor(HeaderBgColor);

            row++;
            int matchedCount = results.Count(r => r.Status == ReconciliationStatus.Matched);
            int likelyCount = results.Count(r => r.Status == ReconciliationStatus.LikelyMatch);
            double matchRate = results.Count > 0 ? (double)(matchedCount + likelyCount) / results.Count * 100 : 0;

            ws.Cells[row, 1].Value = "Match Rate (Matched + Likely):";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 2].Value = Math.Round(matchRate, 1) + "%";
            ws.Cells[row, 2].Style.Font.Bold = true;
            ws.Cells[row, 2].Style.Font.Color.SetColor(matchRate >= 70 ? Color.Green : Color.Red);

            row++;
            decimal totalDiff = results.Sum(r => Math.Abs(r.ExpectedGST - r.ActualGST));
            ws.Cells[row, 1].Value = "Total GST Difference:";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 2].Value = totalDiff;
            ws.Cells[row, 2].Style.Font.Bold = true;
            ws.Cells[row, 2].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[row, 2].Style.Font.Color.SetColor(totalDiff > 0 ? Color.Red : Color.Green);

            row++;
            double avgScore = results.Count > 0 ? results.Average(r => r.FinalScore) : 0;
            ws.Cells[row, 1].Value = "Average Match Score:";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 2].Value = Math.Round(avgScore, 1) + "/100";
            ws.Cells[row, 2].Style.Font.Bold = true;

            // Auto-fit
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 22);
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 18);
            ws.Column(5).Width = Math.Max(ws.Column(5).Width, 16);
            ws.Column(6).Width = Math.Max(ws.Column(6).Width, 16);

            // Print
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
        }

        // ================================================================
        // Detailed Data Sheet — Full Record Detail
        // ================================================================

        /// <summary>
        /// Creates a detailed data worksheet with full columns matching the Reports page view.
        /// When showStatusColumn is true (All Records sheet), includes the status column.
        /// Each row includes: S.No, Status, RERA Name, GST Name, Expected GST, Actual GST,
        /// Difference, Name Score, Amount Score, Final Score.
        /// </summary>
        private void CreateDetailedDataSheet(ExcelPackage package, string sheetName,
            List<ComparisonResult> results, bool showStatusColumn)
        {
            var ws = package.Workbook.Worksheets.Add(sheetName);

            // --- Title Row ---
            ws.Cells["A1"].Value = sheetName + " - GST vs RERA Reconciliation";
            ws.Cells["A1"].Style.Font.Size = 14;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Color.SetColor(HeaderBgColor);

            ws.Cells["A2"].Value = results.Count + " records | Generated: " + DateTime.Now.ToString("dd MMM yyyy HH:mm");
            ws.Cells["A2"].Style.Font.Italic = true;
            ws.Cells["A2"].Style.Font.Size = 9;
            ws.Cells["A2"].Style.Font.Color.SetColor(Color.Gray);

            // --- Build column definitions based on context ---
            var columns = new List<ColumnDef>();
            columns.Add(new ColumnDef("S.No", 7, ExcelHorizontalAlignment.Center));

            if (showStatusColumn)
            {
                columns.Add(new ColumnDef("Status", 17, ExcelHorizontalAlignment.Center));
            }

            columns.Add(new ColumnDef("RERA Name", 28, ExcelHorizontalAlignment.Left));
            columns.Add(new ColumnDef("GST Name", 28, ExcelHorizontalAlignment.Left));
            columns.Add(new ColumnDef("Expected GST", 16, ExcelHorizontalAlignment.Right));
            columns.Add(new ColumnDef("Actual GST", 16, ExcelHorizontalAlignment.Right));
            columns.Add(new ColumnDef("Difference", 14, ExcelHorizontalAlignment.Right));
            columns.Add(new ColumnDef("Name Score", 12, ExcelHorizontalAlignment.Center));
            columns.Add(new ColumnDef("Amt Score", 12, ExcelHorizontalAlignment.Center));
            columns.Add(new ColumnDef("Final Score", 12, ExcelHorizontalAlignment.Center));

            int colCount = columns.Count;
            int headerRow = 4;

            // Merge title across all columns
            ws.Cells[1, 1, 1, colCount].Merge = true;
            ws.Cells[2, 1, 2, colCount].Merge = true;

            // --- Column Headers ---
            for (int c = 0; c < colCount; c++)
            {
                ws.Cells[headerRow, c + 1].Value = columns[c].HeaderText;
            }
            using (var headerRange = ws.Cells[headerRow, 1, headerRow, colCount])
            {
                StyleHeader(headerRange, HeaderBgColor);
            }

            // --- Data Rows ---
            int row = headerRow + 1;
            int sno = 1;
            foreach (var result in results)
            {
                int col = 1;
                decimal diff = result.ExpectedGST - result.ActualGST;

                // S.No
                ws.Cells[row, col].Value = sno;
                col++;

                // Status (optional)
                if (showStatusColumn)
                {
                    ws.Cells[row, col].Value = ReconciliationStatus.DisplayLabel(result.Status);
                    col++;
                }

                // RERA Name
                ws.Cells[row, col].Value = !string.IsNullOrEmpty(result.RERAName) ? result.RERAName : "\u2014";
                col++;

                // GST Name
                ws.Cells[row, col].Value = !string.IsNullOrEmpty(result.GSTName) ? result.GSTName : "\u2014";
                col++;

                // Expected GST
                ws.Cells[row, col].Value = result.ExpectedGST;
                ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                col++;

                // Actual GST
                ws.Cells[row, col].Value = result.ActualGST;
                ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                col++;

                // Difference
                ws.Cells[row, col].Value = diff;
                ws.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                if (diff != 0)
                {
                    ws.Cells[row, col].Style.Font.Color.SetColor(Color.Red);
                    ws.Cells[row, col].Style.Font.Bold = true;
                }
                else
                {
                    ws.Cells[row, col].Style.Font.Color.SetColor(Color.FromArgb(28, 128, 65));
                }
                col++;

                // Name Score
                ws.Cells[row, col].Value = result.NameScore;
                if (result.NameScore >= 90) ws.Cells[row, col].Style.Font.Color.SetColor(Color.Green);
                else if (result.NameScore >= 70) ws.Cells[row, col].Style.Font.Color.SetColor(Color.FromArgb(0, 123, 255));
                col++;

                // Amount Score
                ws.Cells[row, col].Value = result.AmountScore;
                if (result.AmountScore >= 90) ws.Cells[row, col].Style.Font.Color.SetColor(Color.Green);
                else if (result.AmountScore >= 70) ws.Cells[row, col].Style.Font.Color.SetColor(Color.FromArgb(0, 123, 255));
                col++;

                // Final Score
                ws.Cells[row, col].Value = result.FinalScore;
                ws.Cells[row, col].Style.Font.Bold = true;
                if (result.FinalScore >= 90) ws.Cells[row, col].Style.Font.Color.SetColor(Color.Green);
                else if (result.FinalScore >= 70) ws.Cells[row, col].Style.Font.Color.SetColor(Color.FromArgb(0, 123, 255));
                else if (result.FinalScore > 0) ws.Cells[row, col].Style.Font.Color.SetColor(Color.FromArgb(108, 117, 125));

                // --- Row color by status ---
                Color rowColor = GetStatusColor(result.Status, row);
                using (var rowRange = ws.Cells[row, 1, row, colCount])
                {
                    rowRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    rowRange.Style.Fill.BackgroundColor.SetColor(rowColor);
                    AddThinBorder(rowRange);
                }

                row++;
                sno++;
            }

            // --- Totals Row ---
            if (results.Count > 0)
            {
                // Find the column indices for Expected, Actual, Difference
                int expectedCol = showStatusColumn ? 5 : 4;
                int actualCol = expectedCol + 1;
                int diffCol = actualCol + 1;

                ws.Cells[row, expectedCol - 1].Value = "TOTAL";
                ws.Cells[row, expectedCol - 1].Style.Font.Bold = true;
                ws.Cells[row, expectedCol - 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                ws.Cells[row, expectedCol].Value = results.Sum(r => r.ExpectedGST);
                ws.Cells[row, expectedCol].Style.Font.Bold = true;
                ws.Cells[row, expectedCol].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[row, actualCol].Value = results.Sum(r => r.ActualGST);
                ws.Cells[row, actualCol].Style.Font.Bold = true;
                ws.Cells[row, actualCol].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[row, diffCol].Value = results.Sum(r => r.ExpectedGST - r.ActualGST);
                ws.Cells[row, diffCol].Style.Font.Bold = true;
                ws.Cells[row, diffCol].Style.Numberformat.Format = "#,##0.00";

                // Average scores
                int nameScoreCol = diffCol + 1;
                ws.Cells[row, nameScoreCol].Value = Math.Round(results.Average(r => r.NameScore), 0);
                ws.Cells[row, nameScoreCol].Style.Font.Bold = true;
                ws.Cells[row, nameScoreCol + 1].Value = Math.Round(results.Average(r => r.AmountScore), 0);
                ws.Cells[row, nameScoreCol + 1].Style.Font.Bold = true;
                ws.Cells[row, nameScoreCol + 2].Value = Math.Round(results.Average(r => r.FinalScore), 0);
                ws.Cells[row, nameScoreCol + 2].Style.Font.Bold = true;

                using (var totalRange = ws.Cells[row, 1, row, colCount])
                {
                    totalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    totalRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(230, 230, 230));
                    totalRange.Style.Border.Top.Style = ExcelBorderStyle.Double;
                    AddThinBorder(totalRange);
                }

                // --- Record Count Footer ---
                row += 2;
                ws.Cells[row, 1].Value = "Total Records: " + results.Count;
                ws.Cells[row, 1].Style.Font.Italic = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(Color.Gray);

                if (showStatusColumn)
                {
                    // Add per-status summary in footer
                    row++;
                    foreach (var def in StatusDefinitions)
                    {
                        int statusCount = results.Count(r => def.StatusValues.Contains(r.Status));
                        if (statusCount > 0)
                        {
                            ws.Cells[row, 1].Value = def.SheetName + ": " + statusCount;
                            ws.Cells[row, 1].Style.Font.Italic = true;
                            ws.Cells[row, 1].Style.Font.Color.SetColor(def.AccentColor);
                            row++;
                        }
                    }
                }
            }

            // --- Column widths and alignment ---
            for (int c = 0; c < colCount; c++)
            {
                ws.Column(c + 1).Width = columns[c].MinWidth;
                ws.Cells[headerRow, c + 1, row, c + 1].Style.HorizontalAlignment = columns[c].Alignment;
            }

            // Auto-fit then enforce minimums
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            for (int c = 0; c < colCount; c++)
            {
                ws.Column(c + 1).Width = Math.Max(ws.Column(c + 1).Width, columns[c].MinWidth);
            }

            // --- Freeze header ---
            ws.View.FreezePanes(headerRow + 1, 1);

            // --- Print settings ---
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.RepeatRows = ws.Cells[headerRow + ":" + headerRow];
        }

        // ================================================================
        // Styling Helpers
        // ================================================================

        private static void StyleHeader(ExcelRange range, Color bgColor)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.Size = 11;
            range.Style.Font.Color.SetColor(HeaderFontColor);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(bgColor);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.WrapText = false;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
            range.Style.Border.Bottom.Color.SetColor(Color.FromArgb(233, 69, 96));
        }

        private static void AddThinBorder(ExcelRange range)
        {
            range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Top.Color.SetColor(Color.FromArgb(222, 226, 230));
            range.Style.Border.Bottom.Color.SetColor(Color.FromArgb(222, 226, 230));
            range.Style.Border.Left.Color.SetColor(Color.FromArgb(222, 226, 230));
            range.Style.Border.Right.Color.SetColor(Color.FromArgb(222, 226, 230));
        }

        private static Color GetStatusColor(string status, int row)
        {
            switch (status)
            {
                case ReconciliationStatus.Matched:
                    return MatchedRowColor;
                case ReconciliationStatus.LikelyMatch:
                    return LikelyMatchRowColor;
                case ReconciliationStatus.GstMismatch:
                    return MismatchRowColor;
                case ReconciliationStatus.MissingInGst:
                case ReconciliationStatus.ReraNotGst:
                    return MissingInGstRowColor;
                case ReconciliationStatus.MissingInRera:
                case ReconciliationStatus.GstNotRera:
                    return MissingInReraRowColor;
                case ReconciliationStatus.PossibleMatch:
                    return PossibleMatchRowColor;
                default:
                    return row % 2 == 0 ? AlternateRowColor : Color.White;
            }
        }

        // ================================================================
        // Internal Data Structures
        // ================================================================

        /// <summary>Defines a status group for summary and per-sheet export.</summary>
        private class StatusDefinition
        {
            public string SheetName { get; private set; }
            public string[] StatusValues { get; private set; }
            public Color RowColor { get; private set; }
            public Color AccentColor { get; private set; }

            public StatusDefinition(string sheetName, string[] statusValues, Color rowColor, Color accentColor)
            {
                SheetName = sheetName;
                StatusValues = statusValues;
                RowColor = rowColor;
                AccentColor = accentColor;
            }
        }

        /// <summary>Defines a column for the data sheet.</summary>
        private class ColumnDef
        {
            public string HeaderText { get; private set; }
            public int MinWidth { get; private set; }
            public ExcelHorizontalAlignment Alignment { get; private set; }

            public ColumnDef(string header, int minWidth, ExcelHorizontalAlignment alignment)
            {
                HeaderText = header;
                MinWidth = minWidth;
                Alignment = alignment;
            }
        }
    }
}
