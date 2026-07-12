using PS.APP.Localization;
using PS_SZC.Data;

namespace PS_SZC.Services;

public readonly record struct BillingMonth(int Year, int Month)
{
    public static BillingMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public static BillingMonth Current => FromDate(DateOnly.FromDateTime(DateTime.Today));

    public DateOnly ToFirstDay() => new(Year, Month, 1);

    public BillingMonth AddMonths(int count)
    {
        var date = ToFirstDay().AddMonths(count);
        return new BillingMonth(date.Year, date.Month);
    }

    public int CompareTo(BillingMonth other)
    {
        var yearCompare = Year.CompareTo(other.Year);
        return yearCompare != 0 ? yearCompare : Month.CompareTo(other.Month);
    }

    public bool IsOnOrBefore(BillingMonth other) => CompareTo(other) <= 0;

    public override string ToString() => $"{Year}-{Month:D2}";
}

public sealed record MonthlyCharge(
    BillingMonth Month,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount);

public sealed record FamilyBalanceSummary(
    int FamilyId,
    string DisplayName,
    decimal StartingBalance,
    decimal TotalTransfers,
    decimal TotalGrossCharges,
    decimal TotalDiscounts,
    decimal TotalNetCharges,
    decimal CurrentBalance,
    IReadOnlyList<MonthlyCharge> MonthlyCharges)
{
    public bool IsOverpaid => CurrentBalance > 0;

    public bool IsUnderpaid => CurrentBalance < 0;

    public decimal OverpaidAmount => IsOverpaid ? CurrentBalance : 0;

    public decimal UnderpaidAmount => IsUnderpaid ? -CurrentBalance : 0;
}

public static class PaymentBalanceService
{
    public static BillingMonth? GetFamilyBillingStartMonth(int familyId, IReadOnlyList<FamilyPrice> familyPrices)
    {
        BillingMonth? start = null;

        foreach (var price in familyPrices.Where(x => x.FamilyId == familyId))
        {
            var month = new BillingMonth(price.EffectiveYear, price.EffectiveMonth);
            start = start == null || month.CompareTo(start.Value) < 0 ? month : start;
        }

        return start;
    }

    public static decimal GetMonthlyPrice(int familyId, BillingMonth month, IReadOnlyList<FamilyPrice> familyPrices) =>
        familyPrices
            .Where(x => x.FamilyId == familyId)
            .Select(x => new { Entry = x, Month = new BillingMonth(x.EffectiveYear, x.EffectiveMonth) })
            .Where(x => x.Month.IsOnOrBefore(month))
            .OrderByDescending(x => x.Month.Year)
            .ThenByDescending(x => x.Month.Month)
            .Select(x => x.Entry.Amount)
            .FirstOrDefault();

    public static decimal GetMonthlyDiscount(
        int familyId,
        BillingMonth month,
        IReadOnlyList<FamilyDiscount> discounts) =>
        discounts.FirstOrDefault(x =>
            x.FamilyId == familyId && x.Year == month.Year && x.Month == month.Month)?.Amount ?? 0;

    public static BillingMonth? GetPriceAppliesUntilMonth(FamilyPrice price, IReadOnlyList<FamilyPrice> familyPrices)
    {
        var current = new BillingMonth(price.EffectiveYear, price.EffectiveMonth);
        BillingMonth? nextMonth = null;

        foreach (var entry in familyPrices.Where(x => x.FamilyId == price.FamilyId))
        {
            var month = new BillingMonth(entry.EffectiveYear, entry.EffectiveMonth);
            if (month.CompareTo(current) > 0 && (nextMonth == null || month.CompareTo(nextMonth.Value) < 0))
                nextMonth = month;
        }

        return nextMonth == null ? null : nextMonth.Value.AddMonths(-1);
    }

    public static string FormatPriceAppliesUntil(FamilyPrice price, IReadOnlyList<FamilyPrice> familyPrices)
    {
        var until = GetPriceAppliesUntilMonth(price, familyPrices);
        return until == null
            ? LocalizedString.FromId("Family.PriceOngoing")
            : until.Value.ToString();
    }

    public static FamilyBalanceSummary CalculateFamilyBalance(
        Family family,
        IReadOnlyList<FamilyPrice> familyPrices,
        IReadOnlyList<FamilyDiscount> discounts,
        IReadOnlyList<Transfer> transfers,
        BillingMonth throughMonth)
    {
        var familyPricesForFamily = familyPrices.Where(x => x.FamilyId == family.Id).ToList();
        var discountsForFamily = discounts.Where(x => x.FamilyId == family.Id).ToList();
        var transfersForFamily = transfers.Where(x => x.FamilyId == family.Id).ToList();
        var start = GetFamilyBillingStartMonth(family.Id, familyPricesForFamily);
        var monthlyCharges = new List<MonthlyCharge>();

        if (start != null)
        {
            for (var month = start.Value; month.IsOnOrBefore(throughMonth); month = month.AddMonths(1))
            {
                var gross = GetMonthlyPrice(family.Id, month, familyPricesForFamily);
                if (gross <= 0)
                    continue;

                var discount = GetMonthlyDiscount(family.Id, month, discountsForFamily);
                if (discount > gross)
                    discount = gross;

                monthlyCharges.Add(new MonthlyCharge(month, gross, discount, gross - discount));
            }
        }

        var totalTransfers = transfersForFamily.Sum(x => x.Amount);
        var totalGross = monthlyCharges.Sum(x => x.GrossAmount);
        var totalDiscounts = monthlyCharges.Sum(x => x.DiscountAmount);
        var totalNet = monthlyCharges.Sum(x => x.NetAmount);
        var currentBalance = family.StartingBalance + totalTransfers - totalNet;

        return new FamilyBalanceSummary(
            family.Id,
            BuildFamilyDisplayName(family),
            family.StartingBalance,
            totalTransfers,
            totalGross,
            totalDiscounts,
            totalNet,
            currentBalance,
            monthlyCharges);
    }

    public static string BuildFamilyDisplayName(Family family)
    {
        var parents = family.Parents
            .OrderBy(x => x.ParentIndex)
            .Where(x => !string.IsNullOrWhiteSpace(x.LastName) || !string.IsNullOrWhiteSpace(x.FirstName))
            .Select(x => $"{x.FirstName} {x.LastName}".Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (parents.Count > 0)
            return string.Join(" & ", parents);

        var child = family.Children.FirstOrDefault();
        if (child != null)
            return $"{child.LastName} ({child.FirstName})";

        return $"Family #{family.Id}";
    }
}
