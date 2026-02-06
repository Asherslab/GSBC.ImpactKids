using System.Globalization;
using FuzzySharp;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class FuzzyLinq
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Fuzzy searches over multiple properties (any type). Each property is ToString()'d (InvariantCulture),
        /// concatenated, and compared with the query via TokenSetRatio.
        /// </summary>
        public IEnumerable<T> FuzzySearch(
            string?                   query,
            int                       threshold   = 72,
            bool                      orderByBest = true,
            Func<string, string>?     normalize   = null,
            params Func<T, object?>[] fields
        )
        {
            if (fields.Length == 0) return source;               // no fields => pass-through
            if (string.IsNullOrWhiteSpace(query)) return source; // empty query => pass-through

            normalize ??= DefaultNormalize;
            string q = normalize(query);

            var scored = source
                .Select(item =>
                {
                    string joined = JoinFields(item, fields, normalize);
                    int    score  = Fuzz.TokenSetRatio(q, joined);
                    return new { item, score };
                })
                .Where(x => x.score >= threshold);

            return orderByBest
                ? scored.OrderByDescending(x => x.score).Select(x => x.item)
                : scored.Select(x => x.item);
        }

        /// <summary>
        /// Returns items with their fuzzy score (0..100) for UI ranking, pagination, etc.
        /// </summary>
        public IEnumerable<(T Item, int Score)> FuzzySearchWithScores(
            string                    query,
            int                       threshold = 72,
            Func<string, string>?     normalize = null,
            params Func<T, object?>[] fields
        )
        {
            if (fields.Length == 0) return [];
            if (string.IsNullOrWhiteSpace(query)) return source.Select(x => (x, 100));

            normalize ??= DefaultNormalize;
            string q = normalize(query);

            return source
                .Select(item =>
                {
                    string joined = JoinFields(item, fields, normalize);
                    int    score  = Fuzz.TokenSetRatio(q, joined);
                    return (item, score);
                })
                .Where(x => x.score >= threshold)
                .OrderByDescending(x => x.score);
        }

        /// <summary>
        /// Weighted variant: assign relative importance per field (same length as fields).
        /// </summary>
        public IEnumerable<T> FuzzySearchWeighted(
            string                                              query,
            int                                                 threshold   = 72,
            bool                                                orderByBest = true,
            Func<string, string>?                               normalize   = null,
            params (Func<T, object?> selector, double weight)[] weightedFields
        )
        {
            if (weightedFields.Length == 0 || string.IsNullOrWhiteSpace(query)) return source;

            normalize ??= DefaultNormalize;
            string q           = normalize(query);
            double totalWeight = weightedFields.Sum(f => Math.Max(f.weight, 0.0));
            if (totalWeight <= 0.0) totalWeight = 1.0;

            var scored = source
                .Select(item =>
                {
                    // compute field-level scores and blend by weights
                    double blended = 0.0;
                    foreach ((Func<T, object?> sel, double w) in weightedFields)
                    {
                        string text = ToInvariantString(sel(item));
                        text = normalize(text);
                        int score = string.IsNullOrEmpty(text) ? 0 : Fuzz.TokenSetRatio(q, text);
                        blended += score * Math.Max(w, 0.0);
                    }

                    int finalScore = (int)Math.Round(blended / totalWeight);
                    return new { item, score = finalScore };
                })
                .Where(x => x.score >= threshold);

            return orderByBest
                ? scored.OrderByDescending(x => x.score).Select(x => x.item)
                : scored.Select(x => x.item);
        }
    }

    // --- helpers ---
    private static string DefaultNormalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // basic normalisation: trim, lowercase, collapse whitespace
        string t = s.Trim().ToLowerInvariant();
        return string.Join(' ', t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string JoinFields<T>(T item, Func<T, object?>[] fields, Func<string, string> normalize)
    {
        IEnumerable<string> parts = fields
            .Select(f => normalize(ToInvariantString(f(item))))
            .Where(p => !string.IsNullOrEmpty(p));
        return string.Join(' ', parts);
    }

    private static string ToInvariantString(object? value)
    {
        if (value is null) return string.Empty;
        return value switch
        {
            IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
            _                => value.ToString() ?? string.Empty
        };
    }
}