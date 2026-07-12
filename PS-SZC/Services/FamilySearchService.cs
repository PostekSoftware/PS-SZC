using PS_SZC.Data;

namespace PS_SZC.Services;

public static class FamilySearchService
{
    public static string BuildSearchText(Family family)
    {
        var parts = new List<string> { PaymentBalanceService.BuildFamilyDisplayName(family), family.Id.ToString() };

        foreach (var parent in family.Parents.OrderBy(x => x.ParentIndex))
        {
            AddPart(parts, parent.FirstName);
            AddPart(parts, parent.LastName);
            AddPart(parts, parent.Pesel);
        }

        foreach (var child in family.Children.OrderBy(x => x.Id))
        {
            AddPart(parts, child.FirstName);
            AddPart(parts, child.LastName);
            AddPart(parts, child.Pesel);
            parts.Add(child.BirthDate.ToString("yyyy-MM-dd"));
        }

        return string.Join(' ', parts);
    }

    public static bool Matches(Family family, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var haystack = BuildSearchText(family);
        foreach (var term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (haystack.Contains(term, StringComparison.OrdinalIgnoreCase))
                continue;

            return false;
        }

        return true;
    }

    public static IEnumerable<Family> Filter(IEnumerable<Family> families, string query) =>
        families.Where(family => Matches(family, query));

    private static void AddPart(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }
}
