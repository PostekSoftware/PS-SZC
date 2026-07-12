using PS.APP.Localization;
using System.Globalization;

namespace PS_SZC.Services;

public static class ReportFilterSummary
{
    public static IReadOnlyList<string> Build(
        ReportKind kind,
        ReportFilters filters,
        string familyLabel,
        BillingMonth fromMonth,
        BillingMonth toMonth)
    {
        if (fromMonth.CompareTo(toMonth) > 0)
            (fromMonth, toMonth) = (toMonth, fromMonth);

        var lines = new List<string>
        {
            LocalizedString.FromId("Report.FilterSubtitle.Type", () => LocalizedString.FromId(GetKindKey(kind))),
            LocalizedString.FromId("Report.FilterSubtitle.Family", () => familyLabel),
            LocalizedString.FromId("Report.FilterSubtitle.DateRange", () => FormatDateRange(fromMonth, toMonth))
        };

        if (!string.IsNullOrWhiteSpace(filters.FamilySearch))
        {
            lines.Add(LocalizedString.FromId(
                "Report.FilterSubtitle.Search",
                () => filters.FamilySearch.Trim()));
        }

        switch (kind)
        {
            case ReportKind.DuesByMonth:
                lines.Add(LocalizedString.FromId(
                    "Report.FilterSubtitle.HideZeroValue",
                    () => FormatYesNo(filters.HideZeroAmountRows)));
                lines.Add(LocalizedString.FromId(
                    "Report.FilterSubtitle.WithDiscountsValue",
                    () => FormatYesNo(filters.OnlyRowsWithDiscounts)));
                break;
            case ReportKind.PaymentsByMonth:
                lines.Add(LocalizedString.FromId(
                    "Report.FilterSubtitle.HideZeroValue",
                    () => FormatYesNo(filters.HideZeroAmountRows)));
                lines.Add(LocalizedString.FromId(
                    "Report.FilterSubtitle.MinPaymentValue",
                    () => FormatMinimumPayment(filters.MinimumPaymentAmount)));
                break;
            case ReportKind.AccountStatus:
                lines.Add(LocalizedString.FromId(
                    "Report.FilterSubtitle.Status",
                    () => FormatBalanceFilter(filters.BalanceFilter)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return lines;
    }

    public static string FormatBalanceFilter(ReportBalanceFilter filter) => filter switch
    {
        ReportBalanceFilter.Overpaid => LocalizedString.FromId("Summary.Filter.Overpaid"),
        ReportBalanceFilter.Underpaid => LocalizedString.FromId("Summary.Filter.Underpaid"),
        ReportBalanceFilter.Settled => LocalizedString.FromId("Summary.Filter.Settled"),
        _ => LocalizedString.FromId("Summary.Filter.All")
    };

    private static string GetKindKey(ReportKind kind) => kind switch
    {
        ReportKind.DuesByMonth => "Report.Kind.DuesByMonth",
        ReportKind.PaymentsByMonth => "Report.Kind.PaymentsByMonth",
        ReportKind.AccountStatus => "Report.Kind.AccountStatus",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string FormatDateRange(BillingMonth fromMonth, BillingMonth toMonth) =>
        fromMonth.CompareTo(toMonth) == 0
            ? toMonth.ToString()
            : $"{fromMonth} — {toMonth}";

    private static string FormatYesNo(bool value) =>
        LocalizedString.FromId(value ? "Report.Filter.Value.Yes" : "Report.Filter.Value.No");

    private static string FormatMinimumPayment(decimal? amount) =>
        amount is > 0
            ? amount.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : LocalizedString.FromId("Report.Filter.Value.None");
}
