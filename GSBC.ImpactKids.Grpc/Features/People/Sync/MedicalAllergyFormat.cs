namespace GSBC.ImpactKids.Grpc.Features.People.Sync;

/// <summary>
/// The wire format for Elvanto's single free-text medical/allergy custom field.
///
/// Elvanto gives us one text box, but the app stores allergies and medical notes as
/// structured rows. This is the agreed shape that survives a round trip:
///
///     Allergies: Peanuts (SEVERE) - carries EpiPen; Dairy
///     Medical: Asthma (SEVERE) - inhaler in bag; ADHD
///
/// One optional line per section, items separated by "; ", each item
/// <c>Name</c> then optional <c>(SEVERE)</c> then optional <c>- notes</c>.
///
/// Anything that does not parse is not discarded and is not guessed at - it comes back as
/// <see cref="ParsedMedicalAllergy.Unrecognised"/>, and the caller turns it into an "Other"
/// medical note holding the raw text verbatim. Someone typing freehand into Elvanto must
/// never lose what they wrote just because it did not fit our grammar.
///
/// No class here touches the database or Elvanto. It is pure string in, string out, so the
/// round-trip rules can be read and checked on their own.
/// </summary>
public static class MedicalAllergyFormat
{
    public const string AllergiesLabel = "Allergies";
    public const string MedicalLabel   = "Medical";

    private const string SevereMarker = "(SEVERE)";
    private const string ItemSeparator = "; ";
    private const string NotesSeparator = " - ";

    /// <summary>One allergy or medical row, in the neutral form the format deals in.</summary>
    public readonly record struct Item(string Name, bool Severe, string? Notes);

    /// <summary>
    /// The result of reading Elvanto's text box. <paramref name="Unrecognised"/> holds any line
    /// or item we could not interpret, verbatim and in the order it appeared.
    /// </summary>
    public readonly record struct ParsedMedicalAllergy(
        IReadOnlyList<Item>   Allergies,
        IReadOnlyList<Item>   Medical,
        IReadOnlyList<string> Unrecognised
    );

    // ---- compose ------------------------------------------------------------------------

    /// <summary>
    /// Builds the text to push. Returns null when there is nothing to say, so an empty app
    /// record never overwrites Elvanto with an empty string.
    /// </summary>
    public static string? Compose(IEnumerable<Item> allergies, IEnumerable<Item> medical)
    {
        string? allergyLine = ComposeLine(AllergiesLabel, allergies);
        string? medicalLine = ComposeLine(MedicalLabel,   medical);

        return (allergyLine, medicalLine) switch
        {
            (null, null) => null,
            (not null, null) => allergyLine,
            (null, not null) => medicalLine,
            _                => $"{allergyLine}\n{medicalLine}"
        };
    }

    private static string? ComposeLine(string label, IEnumerable<Item> items)
    {
        List<string> rendered = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(RenderItem)
            .ToList();

        return rendered.Count == 0 ? null : $"{label}: {string.Join(ItemSeparator, rendered)}";
    }

    private static string RenderItem(Item item)
    {
        string text = item.Name.Trim();
        if (item.Severe) text += $" {SevereMarker}";

        string? notes = Sanitise(item.Notes);
        if (notes is not null) text += $"{NotesSeparator}{notes}";

        return text;
    }

    /// <summary>
    /// Notes are free text, so they can contain the separators the format relies on. A ";" or a
    /// newline inside a note would make the result re-parse into different items than it was
    /// built from, so both are folded to commas/spaces. Lossy in punctuation only, and only in
    /// the direction that keeps the round trip stable.
    /// </summary>
    private static string? Sanitise(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        string cleaned = notes
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace(";", ",")
            .Trim();

        while (cleaned.Contains("  ")) cleaned = cleaned.Replace("  ", " ");

        return cleaned.Length == 0 ? null : cleaned;
    }

    // ---- parse --------------------------------------------------------------------------

    public static ParsedMedicalAllergy Parse(string? text)
    {
        List<Item>   allergies    = [];
        List<Item>   medical      = [];
        List<string> unrecognised = [];

        if (string.IsNullOrWhiteSpace(text))
            return new ParsedMedicalAllergy(allergies, medical, unrecognised);

        foreach (string rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;

            List<Item>? target = SectionFor(line, out string remainder);
            if (target is null)
            {
                // Not one of our labelled sections - someone typed straight into Elvanto.
                unrecognised.Add(line);
                continue;
            }

            foreach (string rawItem in remainder.Split(';'))
            {
                string itemText = rawItem.Trim();
                if (itemText.Length == 0) continue;

                Item? parsed = ParseItem(itemText);
                if (parsed is null) unrecognised.Add(itemText);
                else target.Add(parsed.Value);
            }
        }

        return new ParsedMedicalAllergy(allergies, medical, unrecognised);

        List<Item>? SectionFor(string line, out string remainder)
        {
            if (StartsWithLabel(line, AllergiesLabel, out remainder)) return allergies;
            if (StartsWithLabel(line, MedicalLabel,   out remainder)) return medical;
            remainder = string.Empty;
            return null;
        }
    }

    private static bool StartsWithLabel(string line, string label, out string remainder)
    {
        if (line.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase))
        {
            remainder = line[(label.Length + 1)..].Trim();
            return true;
        }

        remainder = string.Empty;
        return false;
    }

    private static Item? ParseItem(string itemText)
    {
        string text = itemText;
        string? notes = null;

        // Split on the FIRST " - " so a note may itself contain a dash.
        int notesAt = text.IndexOf(NotesSeparator, StringComparison.Ordinal);
        if (notesAt >= 0)
        {
            notes = text[(notesAt + NotesSeparator.Length)..].Trim();
            text  = text[..notesAt].Trim();
            if (notes.Length == 0) notes = null;
        }

        bool severe = false;
        int severeAt = text.IndexOf(SevereMarker, StringComparison.OrdinalIgnoreCase);
        if (severeAt >= 0)
        {
            severe = true;
            text   = text.Remove(severeAt, SevereMarker.Length).Trim();
        }

        text = text.Trim();

        // A name is the one thing an item cannot do without: "(SEVERE) - hives" names no
        // condition, so it is raw text for a human to read rather than a row to create.
        return text.Length == 0 ? null : new Item(text, severe, notes);
    }
}
