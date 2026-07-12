using Hexa.NET.ImGui;
using PS.APP;
using PS.APP.Dialogs;
using PS.APP.Localization;
using PS.APP.Printing;
using PS_SZC.Services;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PS_SZC.UI;

internal sealed class ReportPreviewForm : Form
{
    private static int _instanceCounter;

    private readonly ReportDocument _document;
    private List<DuesByMonthRow> _duesRows;
    private List<PaymentsByMonthRow> _paymentsRows;
    private List<AccountStatusRow> _accountStatusRows;
    private string _footerMessage = string.Empty;

    private static readonly FileDialogFilter[] CsvFilters =
    [
        new("CSV", "csv"),
        new("All files", "*")
    ];

    private static readonly FileDialogFilter[] PdfFilters =
    [
        new("PDF", "pdf"),
        new("All files", "*")
    ];

    public ReportPreviewForm(ReportDocument document)
    {
        _document = document;
        _duesRows = document.DuesByMonthRows.ToList();
        _paymentsRows = document.PaymentsByMonthRows.ToList();
        _accountStatusRows = document.AccountStatusRows.ToList();

        var instance = Interlocked.Increment(ref _instanceCounter);
        Title = $"{LocalizedString.FromId(document.Title)} #{instance}";
        Size = new Vector2(960, 640);
        Padding = new Vector2(16, 16);
        ShowMenuBar = false;
    }

    public override void Draw()
    {
        DrawAppliedFilters();
        ImGui.Spacing();

        switch (_document.Kind)
        {
            case ReportKind.DuesByMonth:
                DrawDuesByMonthTable();
                break;
            case ReportKind.PaymentsByMonth:
                DrawPaymentsByMonthTable();
                break;
            case ReportKind.AccountStatus:
                DrawAccountStatusTable();
                break;
        }

        DrawFooter();
    }

    private void DrawAppliedFilters()
    {
        ImGui.TextDisabled(LocalizedString.FromId("Report.AppliedFilters"));
        foreach (var filterLine in _document.AppliedFilters)
            ImGui.TextWrapped(filterLine);
    }

    private void DrawDuesByMonthTable() => DrawTable(
        "DuesByMonthReport",
        _duesRows.Count,
        () =>
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Month"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Gross"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Discount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Net"));
            ImGui.TableHeadersRow();

            SortableTable.Sort(_duesRows, ImGui.TableGetSortSpecs(), (row, column) => column switch
            {
                0 => row.FamilyName,
                1 => row.Month.ToString(),
                2 => row.GrossAmount,
                3 => row.DiscountAmount,
                4 => row.NetAmount,
                _ => null
            });

            decimal totalGross = 0;
            decimal totalDiscount = 0;
            decimal totalNet = 0;

            foreach (var row in _duesRows)
            {
                totalGross += row.GrossAmount;
                totalDiscount += row.DiscountAmount;
                totalNet += row.NetAmount;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(row.FamilyName);
                ImGui.TableNextColumn();
                ImGui.Text(row.Month.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.GrossAmount));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.DiscountAmount));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.NetAmount));
            }

            ImGui.EndTable();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{LocalizedString.FromId("Report.Totals")}: {LocalizedString.FromId("Report.Column.Gross")} {FormatMoney(totalGross)} | {LocalizedString.FromId("Report.Column.Discount")} {FormatMoney(totalDiscount)} | {FormatMoney(totalNet)}");
        });

    private void DrawPaymentsByMonthTable() => DrawTable(
        "PaymentsByMonthReport",
        _paymentsRows.Count,
        () =>
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Month"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Amount"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.PaymentCount"));
            ImGui.TableHeadersRow();

            SortableTable.Sort(_paymentsRows, ImGui.TableGetSortSpecs(), (row, column) => column switch
            {
                0 => row.FamilyName,
                1 => row.Month.ToString(),
                2 => row.TotalAmount,
                3 => row.TransferCount,
                _ => null
            });

            decimal totalAmount = 0;
            var totalCount = 0;

            foreach (var row in _paymentsRows)
            {
                totalAmount += row.TotalAmount;
                totalCount += row.TransferCount;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(row.FamilyName);
                ImGui.TableNextColumn();
                ImGui.Text(row.Month.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.TotalAmount));
                ImGui.TableNextColumn();
                ImGui.Text(row.TransferCount.ToString(CultureInfo.InvariantCulture));
            }

            ImGui.EndTable();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"{LocalizedString.FromId("Report.Totals")}: {FormatMoney(totalAmount)} | {LocalizedString.FromId("Report.Column.PaymentCount")} {totalCount}");
        });

    private void DrawAccountStatusTable() => DrawTable(
        "AccountStatusReport",
        _accountStatusRows.Count,
        () =>
        {
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Family"), ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.StartingBalance"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Transfers"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Charges"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Balance"));
            ImGui.TableSetupColumn(LocalizedString.FromId("Report.Column.Status"));
            ImGui.TableHeadersRow();

            SortableTable.Sort(_accountStatusRows, ImGui.TableGetSortSpecs(), (row, column) => column switch
            {
                0 => row.FamilyName,
                1 => row.StartingBalance,
                2 => row.TotalTransfers,
                3 => row.TotalNetCharges,
                4 => row.CurrentBalance,
                5 => row.Status,
                _ => null
            });

            foreach (var row in _accountStatusRows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(row.FamilyName);
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.StartingBalance));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.TotalTransfers));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.TotalNetCharges));
                ImGui.TableNextColumn();
                ImGui.Text(FormatMoney(row.CurrentBalance));
                ImGui.TableNextColumn();
                ImGui.Text(FormatStatus(row));
            }

            ImGui.EndTable();
        });

    private void DrawTable(string id, int rowCount, Action drawTable)
    {
        if (rowCount == 0)
        {
            ImGui.TextDisabled(LocalizedString.FromId("Report.NoData"));
            return;
        }

        var footerHeight = ImGui.GetFrameHeightWithSpacing() * (string.IsNullOrEmpty(_footerMessage) ? 3 : 5);
        if (!ImGui.BeginChild($"ReportTable_{id}", new Vector2(0, -footerHeight)))
            return;

        var columnCount = id switch
        {
            "DuesByMonthReport" => 5,
            "PaymentsByMonthReport" => 4,
            "AccountStatusReport" => 6,
            _ => 1
        };

        if (ImGui.BeginTable(id, columnCount, SortableTable.BaseFlags | ImGuiTableFlags.ScrollY))
            drawTable();

        ImGui.EndChild();
    }

    private static LocalizedString FormatStatus(AccountStatusRow row) => row.Status switch
    {
        ReportBalanceStatus.Overpaid => LocalizedString.FromId(
            "Report.Status.Overpaid",
            () => FormatMoney(row.StatusAmount)),
        ReportBalanceStatus.Underpaid => LocalizedString.FromId(
            "Report.Status.Underpaid",
            () => FormatMoney(row.StatusAmount)),
        _ => LocalizedString.FromId("Report.Status.Settled")
    };

    private void DrawFooter()
    {
        ImGui.Separator();

        if (!string.IsNullOrEmpty(_footerMessage))
        {
            ImGui.TextWrapped(_footerMessage);
            ImGui.Spacing();
        }

        var hasData = ReportCsvExporter.HasData(_document);
        if (!hasData)
            ImGui.BeginDisabled();

        if (ImGui.Button(LocalizedString.FromId("Report.ExportCsv")))
            PromptExportCsv();

        if (!hasData)
            ImGui.EndDisabled();

        ImGui.SameLine();

        if (!hasData)
            ImGui.BeginDisabled();

        if (ImGui.Button(LocalizedString.FromId("Report.ExportPdf")))
            PromptExportPdf();

        if (!hasData)
            ImGui.EndDisabled();

        ImGui.SameLine();

        if (!hasData)
            ImGui.BeginDisabled();

        if (ImGui.Button(LocalizedString.FromId("Report.Print")))
            PrintReport();

        if (!hasData)
            ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(LocalizedString.FromId("Report.Close")))
            Close();
    }

    private void PrintReport()
    {
        if (!ReportPdfExporter.HasData(_document))
        {
            _footerMessage = LocalizedString.FromId("Report.NoData");
            return;
        }

        try
        {
            var tempPath = NativePrintService.CreateTempPdfPath(ReportPdfExporter.SuggestFileName(_document));
            ReportPdfExporter.Build(_document, tempPath);
            NativePrintService.PrintPdf(tempPath);
            _footerMessage = LocalizedString.FromId("Report.PrintSuccess");
        }
        catch (Exception ex)
        {
            _footerMessage = $"{LocalizedString.FromId("Report.PrintFailed")} {ex.Message}";
        }
    }

    private void PromptExportCsv()
    {
        if (!ReportCsvExporter.HasData(_document))
        {
            _footerMessage = LocalizedString.FromId("Report.NoData");
            return;
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ReportCsvExporter.SuggestFileName(_document));

        if (!Application.Instance.ShowSaveFileDialog(CsvFilters, OnExportCsvComplete, defaultPath))
            _footerMessage = LocalizedString.FromId("Report.ExportCsvBusy");
    }

    private void PromptExportPdf()
    {
        if (!ReportPdfExporter.HasData(_document))
        {
            _footerMessage = LocalizedString.FromId("Report.NoData");
            return;
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ReportPdfExporter.SuggestFileName(_document));

        if (!Application.Instance.ShowSaveFileDialog(PdfFilters, OnExportPdfComplete, defaultPath))
            _footerMessage = LocalizedString.FromId("Report.ExportCsvBusy");
    }

    private void OnExportPdfComplete(FileDialogResult result)
    {
        if (result.Kind == FileDialogResultKind.Cancelled)
            return;

        if (result.Kind != FileDialogResultKind.Success || string.IsNullOrWhiteSpace(result.Path))
        {
            _footerMessage = result.ErrorMessage ?? LocalizedString.FromId("Report.ExportPdfFailed");
            return;
        }

        try
        {
            var outputPath = EnsurePdfExtension(result.Path);
            ReportPdfExporter.Build(_document, outputPath);
            _footerMessage = LocalizedString.FromId("Report.ExportPdfSuccess");
        }
        catch (Exception ex)
        {
            _footerMessage = $"{LocalizedString.FromId("Report.ExportPdfFailed")} {ex.Message}";
        }
    }

    private static string EnsurePdfExtension(string filePath) =>
        Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? filePath
            : filePath + ".pdf";

    private void OnExportCsvComplete(FileDialogResult result)
    {
        if (result.Kind == FileDialogResultKind.Cancelled)
            return;

        if (result.Kind != FileDialogResultKind.Success || string.IsNullOrWhiteSpace(result.Path))
        {
            _footerMessage = result.ErrorMessage ?? LocalizedString.FromId("Report.ExportCsvFailed");
            return;
        }

        try
        {
            var csv = ReportCsvExporter.Build(_document);
            File.WriteAllText(result.Path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            _footerMessage = LocalizedString.FromId("Report.ExportCsvSuccess");
        }
        catch (Exception ex)
        {
            _footerMessage = $"{LocalizedString.FromId("Report.ExportCsvFailed")} {ex.Message}";
        }
    }

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
