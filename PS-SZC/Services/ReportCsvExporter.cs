using PS.APP.Localization;
using System.Globalization;
using System.Text;

namespace PS_SZC.Services;

public static class ReportCsvExporter
{
    public static string SuggestFileName(ReportDocument document)
    {
        var baseName = document.Kind switch
        {
            ReportKind.DuesByMonth => "dues-by-month",
            ReportKind.PaymentsByMonth => "payments-by-month",
            ReportKind.AccountStatus => "account-status",
            _ => "report"
        };

        return $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
    }

    public static bool HasData(ReportDocument document) =>
        document.Kind switch
        {
            ReportKind.DuesByMonth => document.DuesByMonthRows.Count > 0,
            ReportKind.PaymentsByMonth => document.PaymentsByMonthRows.Count > 0,
            ReportKind.AccountStatus => document.AccountStatusRows.Count > 0,
            _ => false
        };

    public static string Build(ReportDocument document)
    {
        var builder = new StringBuilder();

        foreach (var filterLine in document.AppliedFilters)
            WriteRow(builder, filterLine);

        if (document.AppliedFilters.Count > 0)
            builder.AppendLine();

        switch (document.Kind)
        {
            case ReportKind.DuesByMonth:
                BuildDuesByMonth(document.DuesByMonthRows, builder);
                break;
            case ReportKind.PaymentsByMonth:
                BuildPaymentsByMonth(document.PaymentsByMonthRows, builder);
                break;
            case ReportKind.AccountStatus:
                BuildAccountStatus(document.AccountStatusRows, builder);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(document), document.Kind, null);
        }

        return builder.ToString();
    }

    private static void BuildDuesByMonth(IReadOnlyList<DuesByMonthRow> rows, StringBuilder builder)
    {
        WriteRow(builder,
            Header("Report.Column.Family"),
            Header("Report.Column.Month"),
            Header("Report.Column.Gross"),
            Header("Report.Column.Discount"),
            Header("Report.Column.Net"));

        foreach (var row in rows)
        {
            WriteRow(builder,
                row.FamilyName,
                row.Month.ToString(),
                FormatMoney(row.GrossAmount),
                FormatMoney(row.DiscountAmount),
                FormatMoney(row.NetAmount));
        }
    }

    private static void BuildPaymentsByMonth(IReadOnlyList<PaymentsByMonthRow> rows, StringBuilder builder)
    {
        WriteRow(builder,
            Header("Report.Column.Family"),
            Header("Report.Column.Month"),
            Header("Report.Column.Amount"),
            Header("Report.Column.PaymentCount"));

        foreach (var row in rows)
        {
            WriteRow(builder,
                row.FamilyName,
                row.Month.ToString(),
                FormatMoney(row.TotalAmount),
                row.TransferCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void BuildAccountStatus(IReadOnlyList<AccountStatusRow> rows, StringBuilder builder)
    {
        WriteRow(builder,
            Header("Report.Column.Family"),
            Header("Report.Column.StartingBalance"),
            Header("Report.Column.Transfers"),
            Header("Report.Column.Charges"),
            Header("Report.Column.Balance"),
            Header("Report.Column.Status"));

        foreach (var row in rows)
        {
            WriteRow(builder,
                row.FamilyName,
                FormatMoney(row.StartingBalance),
                FormatMoney(row.TotalTransfers),
                FormatMoney(row.TotalNetCharges),
                FormatMoney(row.CurrentBalance),
                FormatStatus(row));
        }
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

    private static void WriteRow(StringBuilder builder, params string?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
                builder.Append(',');

            builder.Append(EscapeCell(cells[i]));
        }

        builder.AppendLine();
    }

    private static string EscapeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.Contains(',')
            || value.Contains('"')
            || value.Contains('\n')
            || value.Contains('\r');

        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
