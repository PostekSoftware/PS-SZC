using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PS.APP.Localization;
using System.Globalization;

namespace PS_SZC.Services;

public static class ReportPdfExporter
{
    static ReportPdfExporter()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new ReportPdfFontResolver();
    }

    public static string SuggestFileName(ReportDocument document) =>
        Path.ChangeExtension(ReportCsvExporter.SuggestFileName(document), ".pdf");

    public static bool HasData(ReportDocument document) => ReportCsvExporter.HasData(document);

    public static void Build(ReportDocument document, string outputPath)
    {
        var builder = new PdfReportBuilder(document, DateTime.Now);
        builder.Render();
        builder.Save(outputPath);
    }

    private sealed class PdfReportBuilder
    {
        private const double Margin = 36;
        private const double FooterArea = 34;
        private const double RowHeight = 14;
        private const double HeaderRowHeight = 16;

        private readonly ReportDocument _document;
        private readonly DateTime _generatedAt;
        private readonly PdfDocument _pdf = new();
        private readonly XFont _titleFont = new("ReportSans", 16, XFontStyle.Bold);
        private readonly XFont _subtitleFont = new("ReportSans", 10, XFontStyle.Regular);
        private readonly XFont _headerFont = new("ReportSans", 9, XFontStyle.Bold);
        private readonly XFont _bodyFont = new("ReportSans", 9, XFontStyle.Regular);
        private readonly XFont _footerFont = new("ReportSans", 8, XFontStyle.Regular);

        private PdfPage _page = null!;
        private XGraphics _gfx = null!;
        private double _y;
        private bool _isFirstPage = true;
        private double _contentBottom;

        public PdfReportBuilder(ReportDocument document, DateTime generatedAt)
        {
            _document = document;
            _generatedAt = generatedAt;
        }

        public void Render()
        {
            BeginPage();

            if (_isFirstPage)
            {
                DrawTitleBlock();
                _isFirstPage = false;
            }

            switch (_document.Kind)
            {
                case ReportKind.DuesByMonth:
                    RenderTable(
                        [3, 1, 1, 1, 1],
                        [
                            Header("Report.Column.Family"),
                            Header("Report.Column.Month"),
                            Header("Report.Column.Gross"),
                            Header("Report.Column.Discount"),
                            Header("Report.Column.Net")
                        ],
                        _document.DuesByMonthRows.Select(row => new[]
                        {
                            row.FamilyName,
                            row.Month.ToString(),
                            FormatMoney(row.GrossAmount),
                            FormatMoney(row.DiscountAmount),
                            FormatMoney(row.NetAmount)
                        }),
                        [false, false, true, true, true]);
                    break;
                case ReportKind.PaymentsByMonth:
                    RenderTable(
                        [3, 1, 1, 1],
                        [
                            Header("Report.Column.Family"),
                            Header("Report.Column.Month"),
                            Header("Report.Column.Amount"),
                            Header("Report.Column.PaymentCount")
                        ],
                        _document.PaymentsByMonthRows.Select(row => new[]
                        {
                            row.FamilyName,
                            row.Month.ToString(),
                            FormatMoney(row.TotalAmount),
                            row.TransferCount.ToString(CultureInfo.InvariantCulture)
                        }),
                        [false, false, true, true]);
                    break;
                case ReportKind.AccountStatus:
                    RenderTable(
                        [3, 1, 1, 1, 1, 2],
                        [
                            Header("Report.Column.Family"),
                            Header("Report.Column.StartingBalance"),
                            Header("Report.Column.Transfers"),
                            Header("Report.Column.Charges"),
                            Header("Report.Column.Balance"),
                            Header("Report.Column.Status")
                        ],
                        _document.AccountStatusRows.Select(row => new[]
                        {
                            row.FamilyName,
                            FormatMoney(row.StartingBalance),
                            FormatMoney(row.TotalTransfers),
                            FormatMoney(row.TotalNetCharges),
                            FormatMoney(row.CurrentBalance),
                            FormatStatus(row)
                        }),
                        [false, true, true, true, true, false]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_document), _document.Kind, null);
            }

            EndPage();
        }

        public void Save(string outputPath) => _pdf.Save(outputPath);

        private void DrawTitleBlock()
        {
            var title = LocalizedString.FromId(_document.Title);
            _gfx.DrawString(title, _titleFont, XBrushes.Black, new XRect(ContentLeft, _y, ContentWidth, 20), XStringFormats.TopLeft);
            _y += 22;

            _gfx.DrawString(
                LocalizedString.FromId("Report.AppliedFilters"),
                _headerFont,
                XBrushes.Black,
                new XRect(ContentLeft, _y, ContentWidth, 14),
                XStringFormats.TopLeft);
            _y += 16;

            foreach (var filterLine in _document.AppliedFilters)
            {
                _gfx.DrawString(
                    filterLine,
                    _subtitleFont,
                    XBrushes.Black,
                    new XRect(ContentLeft, _y, ContentWidth, 14),
                    XStringFormats.TopLeft);
                _y += 14;
            }

            _y += 8;
        }

        private void RenderTable(
            int[] columnWeights,
            string[] headers,
            IEnumerable<string[]> rows,
            bool[] alignRight)
        {
            var columnWidths = CalculateColumnWidths(columnWeights);
            DrawHeaderRow(headers, columnWidths);

            foreach (var row in rows)
            {
                if (_y + RowHeight > _contentBottom)
                {
                    EndPage();
                    BeginPage();
                    DrawHeaderRow(headers, columnWidths);
                }

                DrawDataRow(row, columnWidths, alignRight);
            }
        }

        private void DrawHeaderRow(string[] headers, double[] columnWidths)
        {
            var x = ContentLeft;
            for (var i = 0; i < headers.Length; i++)
            {
                var rect = new XRect(x, _y, columnWidths[i], HeaderRowHeight);
                _gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(235, 235, 235)), rect);
                _gfx.DrawRectangle(XPens.LightGray, rect);
                DrawCellText(headers[i], rect, _headerFont, false);
                x += columnWidths[i];
            }

            _y += HeaderRowHeight;
        }

        private void DrawDataRow(string[] cells, double[] columnWidths, bool[] alignRight)
        {
            var x = ContentLeft;
            for (var i = 0; i < cells.Length; i++)
            {
                var rect = new XRect(x, _y, columnWidths[i], RowHeight);
                _gfx.DrawRectangle(XPens.Gainsboro, rect);
                DrawCellText(cells[i], rect, _bodyFont, alignRight[i]);
                x += columnWidths[i];
            }

            _y += RowHeight;
        }

        private void DrawCellText(string text, XRect rect, XFont font, bool alignRight)
        {
            var padded = new XRect(rect.X + 3, rect.Y + 2, rect.Width - 6, rect.Height - 4);
            var formatter = new XTextFormatter(_gfx)
            {
                Alignment = alignRight ? XParagraphAlignment.Right : XParagraphAlignment.Left
            };
            formatter.DrawString(text, font, XBrushes.Black, padded);
        }

        private void BeginPage()
        {
            _page = _pdf.AddPage();
            _page.Width = XUnit.FromMillimeter(297);
            _page.Height = XUnit.FromMillimeter(210);
            _gfx = XGraphics.FromPdfPage(_page);
            _y = Margin;
            _contentBottom = _page.Height - Margin - FooterArea;
        }

        private void EndPage()
        {
            var footerY = _page.Height - Margin;
            var generatedBy = LocalizedString.FromId("Report.Pdf.GeneratedBy");
            var generatedAt = LocalizedString.FromId(
                "Report.Pdf.GeneratedAt",
                () => _generatedAt.ToString("g", CultureInfo.CurrentCulture));

            _gfx.DrawString(
                generatedBy,
                _footerFont,
                XBrushes.Gray,
                new XRect(ContentLeft, footerY - 14, ContentWidth, 12),
                XStringFormats.TopCenter);

            _gfx.DrawString(
                generatedAt,
                _footerFont,
                XBrushes.Gray,
                new XRect(ContentLeft, footerY - 2, ContentWidth, 12),
                XStringFormats.TopCenter);

            _gfx.Dispose();
        }

        private double[] CalculateColumnWidths(int[] weights)
        {
            var totalWeight = weights.Sum();
            return weights.Select(weight => ContentWidth * weight / totalWeight).ToArray();
        }

        private double ContentLeft => Margin;

        private double ContentWidth => _page.Width - Margin * 2;
    }

    private static string Header(string localizationId) => LocalizedString.FromId(localizationId);

    private static string FormatStatus(AccountStatusRow row) => row.Status switch
    {
        ReportBalanceStatus.Overpaid => LocalizedString.FromId(
            "Report.Status.Overpaid",
            () => FormatMoney(row.StatusAmount)),
        ReportBalanceStatus.Underpaid => LocalizedString.FromId(
            "Report.Status.Underpaid",
            () => FormatMoney(row.StatusAmount)),
        _ => LocalizedString.FromId("Report.Status.Settled")
    };

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
