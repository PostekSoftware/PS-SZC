using PS.APP.Localization;
using PS_SZC.Data;

namespace PS_SZC.Services;

public enum ReportKind
{
    DuesByMonth,
    PaymentsByMonth,
    AccountStatus
}

public sealed record DuesByMonthRow(
    int FamilyId,
    string FamilyName,
    BillingMonth Month,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount);

public sealed record PaymentsByMonthRow(
    int FamilyId,
    string FamilyName,
    BillingMonth Month,
    decimal TotalAmount,
    int TransferCount);

public enum ReportBalanceStatus
{
    Settled,
    Overpaid,
    Underpaid
}

public sealed record AccountStatusRow(
    int FamilyId,
    string FamilyName,
    decimal StartingBalance,
    decimal TotalTransfers,
    decimal TotalNetCharges,
    decimal TotalDiscounts,
    decimal CurrentBalance,
    ReportBalanceStatus Status,
    decimal StatusAmount);

public enum ReportBalanceFilter
{
    All,
    Overpaid,
    Underpaid,
    Settled
}

public sealed record ReportFilters(
    int? FamilyId,
    string FamilySearch,
    ReportBalanceFilter BalanceFilter,
    bool HideZeroAmountRows,
    bool OnlyRowsWithDiscounts,
    decimal? MinimumPaymentAmount)
{
    public static ReportFilters Default => new(null, string.Empty, ReportBalanceFilter.All, false, false, null);
}

public sealed record ReportDocument(
    ReportKind Kind,
    string Title,
    string Subtitle,
    IReadOnlyList<string> AppliedFilters,
    IReadOnlyList<DuesByMonthRow> DuesByMonthRows,
    IReadOnlyList<PaymentsByMonthRow> PaymentsByMonthRows,
    IReadOnlyList<AccountStatusRow> AccountStatusRows);

public static class ReportService
{
    public static ReportDocument Build(
        ReportKind kind,
        IReadOnlyList<Family> families,
        IReadOnlyList<FamilyPrice> prices,
        IReadOnlyList<FamilyDiscount> discounts,
        IReadOnlyList<Transfer> transfers,
        BillingMonth fromMonth,
        BillingMonth toMonth,
        ReportFilters filters,
        string allFamiliesLabel) =>
        kind switch
        {
            ReportKind.DuesByMonth => BuildDuesByMonthReport(
                families, prices, discounts, fromMonth, toMonth, filters, allFamiliesLabel),
            ReportKind.PaymentsByMonth => BuildPaymentsByMonthReport(
                families, transfers, fromMonth, toMonth, filters, allFamiliesLabel),
            ReportKind.AccountStatus => BuildAccountStatusReport(
                families, prices, discounts, transfers, fromMonth, toMonth, filters, allFamiliesLabel),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public static ReportDocument BuildDuesByMonthReport(
        IReadOnlyList<Family> families,
        IReadOnlyList<FamilyPrice> prices,
        IReadOnlyList<FamilyDiscount> discounts,
        BillingMonth fromMonth,
        BillingMonth toMonth,
        ReportFilters filters,
        string allFamiliesLabel)
    {
        var (rangeFrom, rangeTo, targetFamilies, familyLabel) = Prepare(
            families, fromMonth, toMonth, filters, allFamiliesLabel);

        var rows = new List<DuesByMonthRow>();

        foreach (var family in targetFamilies)
        {
            var summary = PaymentBalanceService.CalculateFamilyBalance(
                family, prices, discounts, [], rangeTo);

            foreach (var charge in summary.MonthlyCharges)
            {
                if (charge.Month.CompareTo(rangeFrom) < 0 || charge.Month.CompareTo(rangeTo) > 0)
                    continue;

                if (filters.HideZeroAmountRows && charge.NetAmount == 0 && charge.GrossAmount == 0)
                    continue;

                if (filters.OnlyRowsWithDiscounts && charge.DiscountAmount <= 0)
                    continue;

                rows.Add(new DuesByMonthRow(
                    family.Id,
                    summary.DisplayName,
                    charge.Month,
                    charge.GrossAmount,
                    charge.DiscountAmount,
                    charge.NetAmount));
            }
        }

        rows.Sort(SortByFamilyThenMonth);

        var appliedFilters = ReportFilterSummary.Build(
            ReportKind.DuesByMonth, filters, familyLabel, rangeFrom, rangeTo);

        return new ReportDocument(
            ReportKind.DuesByMonth,
            "Report.DuesByMonth.Title",
            string.Join(" | ", appliedFilters),
            appliedFilters,
            rows,
            [],
            []);
    }

    public static ReportDocument BuildPaymentsByMonthReport(
        IReadOnlyList<Family> families,
        IReadOnlyList<Transfer> transfers,
        BillingMonth fromMonth,
        BillingMonth toMonth,
        ReportFilters filters,
        string allFamiliesLabel)
    {
        var (rangeFrom, rangeTo, targetFamilies, familyLabel) = Prepare(
            families, fromMonth, toMonth, filters, allFamiliesLabel);

        var rows = new List<PaymentsByMonthRow>();

        foreach (var family in targetFamilies)
        {
            var displayName = PaymentBalanceService.BuildFamilyDisplayName(family);
            var grouped = transfers
                .Where(x => x.FamilyId == family.Id)
                .Where(x => filters.MinimumPaymentAmount == null || x.Amount >= filters.MinimumPaymentAmount.Value)
                .Select(x => new { Transfer = x, Month = BillingMonth.FromDate(x.TransferDate) })
                .Where(x => x.Month.CompareTo(rangeFrom) >= 0 && x.Month.CompareTo(rangeTo) <= 0)
                .GroupBy(x => x.Month)
                .Select(group => new PaymentsByMonthRow(
                    family.Id,
                    displayName,
                    group.Key,
                    group.Sum(x => x.Transfer.Amount),
                    group.Count()))
                .Where(row => !filters.HideZeroAmountRows || row.TotalAmount != 0)
                .ToList();

            rows.AddRange(grouped);
        }

        rows.Sort((left, right) =>
        {
            var familyCompare = string.Compare(left.FamilyName, right.FamilyName, StringComparison.OrdinalIgnoreCase);
            if (familyCompare != 0)
                return familyCompare;

            return left.Month.CompareTo(right.Month);
        });

        var appliedFilters = ReportFilterSummary.Build(
            ReportKind.PaymentsByMonth, filters, familyLabel, rangeFrom, rangeTo);

        return new ReportDocument(
            ReportKind.PaymentsByMonth,
            "Report.PaymentsByMonth.Title",
            string.Join(" | ", appliedFilters),
            appliedFilters,
            [],
            rows,
            []);
    }

    public static ReportDocument BuildAccountStatusReport(
        IReadOnlyList<Family> families,
        IReadOnlyList<FamilyPrice> prices,
        IReadOnlyList<FamilyDiscount> discounts,
        IReadOnlyList<Transfer> transfers,
        BillingMonth fromMonth,
        BillingMonth toMonth,
        ReportFilters filters,
        string allFamiliesLabel)
    {
        if (fromMonth.CompareTo(toMonth) > 0)
            (fromMonth, toMonth) = (toMonth, fromMonth);

        var throughMonth = toMonth;
        var targetFamilies = SelectFamilies(families, filters);

        var familyLabel = BuildFamilyLabel(families, filters, allFamiliesLabel);

        var rows = targetFamilies
            .Select(family =>
            {
                var summary = PaymentBalanceService.CalculateFamilyBalance(
                    family, prices, discounts, transfers, throughMonth);

                var (status, statusAmount) = summary.IsOverpaid
                    ? (ReportBalanceStatus.Overpaid, summary.OverpaidAmount)
                    : summary.IsUnderpaid
                        ? (ReportBalanceStatus.Underpaid, summary.UnderpaidAmount)
                        : (ReportBalanceStatus.Settled, 0m);

                return new AccountStatusRow(
                    summary.FamilyId,
                    summary.DisplayName,
                    summary.StartingBalance,
                    summary.TotalTransfers,
                    summary.TotalNetCharges,
                    summary.TotalDiscounts,
                    summary.CurrentBalance,
                    status,
                    statusAmount);
            })
            .Where(row => MatchesBalanceFilter(row, filters.BalanceFilter))
            .OrderBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var appliedFilters = ReportFilterSummary.Build(
            ReportKind.AccountStatus, filters, familyLabel, fromMonth, toMonth);

        return new ReportDocument(
            ReportKind.AccountStatus,
            "Report.AccountStatus.Title",
            string.Join(" | ", appliedFilters),
            appliedFilters,
            [],
            [],
            rows);
    }

    private static Comparison<DuesByMonthRow> SortByFamilyThenMonth => (left, right) =>
    {
        var familyCompare = string.Compare(left.FamilyName, right.FamilyName, StringComparison.OrdinalIgnoreCase);
        return familyCompare != 0 ? familyCompare : left.Month.CompareTo(right.Month);
    };

    private static (BillingMonth From, BillingMonth To, List<Family> Families, string FamilyLabel) Prepare(
        IReadOnlyList<Family> families,
        BillingMonth fromMonth,
        BillingMonth toMonth,
        ReportFilters filters,
        string allFamiliesLabel)
    {
        if (fromMonth.CompareTo(toMonth) > 0)
            (fromMonth, toMonth) = (toMonth, fromMonth);

        var targetFamilies = SelectFamilies(families, filters);
        var familyLabel = BuildFamilyLabel(families, filters, allFamiliesLabel);

        return (fromMonth, toMonth, targetFamilies, familyLabel);
    }

    private static List<Family> SelectFamilies(IReadOnlyList<Family> families, ReportFilters filters)
    {
        IEnumerable<Family> selected = filters.FamilyId == null
            ? families
            : families.Where(x => x.Id == filters.FamilyId);

        if (!string.IsNullOrWhiteSpace(filters.FamilySearch))
            selected = selected.Where(family => FamilySearchService.Matches(family, filters.FamilySearch));

        return selected.ToList();
    }

    private static string BuildFamilyLabel(
        IReadOnlyList<Family> families,
        ReportFilters filters,
        string allFamiliesLabel)
    {
        if (filters.FamilyId != null)
        {
            var family = families.FirstOrDefault(x => x.Id == filters.FamilyId);
            return family == null
                ? allFamiliesLabel
                : PaymentBalanceService.BuildFamilyDisplayName(family);
        }

        if (!string.IsNullOrWhiteSpace(filters.FamilySearch))
            return LocalizedString.FromId("Report.AllFamiliesFiltered");

        return allFamiliesLabel;
    }

    private static bool MatchesBalanceFilter(AccountStatusRow row, ReportBalanceFilter filter) =>
        filter switch
        {
            ReportBalanceFilter.Overpaid => row.Status == ReportBalanceStatus.Overpaid,
            ReportBalanceFilter.Underpaid => row.Status == ReportBalanceStatus.Underpaid,
            ReportBalanceFilter.Settled => row.Status == ReportBalanceStatus.Settled,
            _ => true
        };
}
