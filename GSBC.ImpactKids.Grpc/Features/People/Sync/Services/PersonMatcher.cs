using System.Globalization;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public class PersonMatcher : IPersonMatcher
{
    // Elvanto stores birthdays in local AEST; DB stores DateOfBirth as UTC — convert before comparing
    private static readonly TimeZoneInfo AestZone =
        TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public SyncMatchCandidate? FindBestMatch(ElvantoPerson elvantoPerson, IReadOnlyList<DbPerson> candidates)
    {
        if (candidates.Count == 0) return null;

        string elvFirst = Normalize(elvantoPerson.FirstName);
        string elvLast  = Normalize(elvantoPerson.LastName);
        string elvEmail = Normalize(elvantoPerson.Email);

        string? elvDob  = elvantoPerson.Birthday;

        SyncMatchCandidate? best = null;

        foreach (DbPerson candidate in candidates)
        {
            string cFirst = Normalize(candidate.FirstName);
            string cLast  = Normalize(candidate.LastName);
            string cEmail = Normalize(candidate.Email);
            string? cDob  = candidate.DateOfBirth.HasValue
                ? TimeZoneInfo.ConvertTime(candidate.DateOfBirth.Value, AestZone)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;

            bool dobMatch   = elvDob is not null && cDob is not null && elvDob == cDob;
            bool exactName  = elvFirst == cFirst && elvLast == cLast;
            bool fuzzyName  = !exactName && Levenshtein(elvFirst, cFirst) <= 2 && Levenshtein(elvLast, cLast) <= 2;
            bool emailMatch = elvEmail.Length > 0 && elvEmail == cEmail;

            int confidence;
            string strategy;

            if (exactName && dobMatch)          { confidence = 100; strategy = "ExactNameAndDob"; }
            else if (fuzzyName && dobMatch)     { confidence = 90;  strategy = "FuzzyNameAndDob"; }
            else if (exactName)                 { confidence = 75;  strategy = "ExactName"; }
            else if (emailMatch)                { confidence = 50;  strategy = "Email"; }
            else continue;

            if (best is null || confidence > best.Confidence)
                best = new SyncMatchCandidate(candidate, confidence, strategy);
        }

        return best;
    }

    private static string Normalize(string? s) =>
        (s ?? "").Trim().ToLowerInvariant();

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        for (int j = 1; j <= b.Length; j++)
        {
            int cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }

        return d[a.Length, b.Length];
    }
}
