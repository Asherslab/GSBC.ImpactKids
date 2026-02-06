using System.Text.RegularExpressions;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public partial record BibleVerse : IIdentifiable
{
    public Guid Id { get; init; }

    public          int    BookNumber    { get; init; }
    public required string BookName      { get; init; }
    public          int    ChapterNumber { get; init; }
    public          int    VerseNumber   { get; init; }

    public required string Verse { get; init; }

    public string Reference() => $"{BookName} {ChapterNumber}:{VerseNumber}";

    public static IEnumerable<BibleVerse> BibleVerseSearch(string search, IEnumerable<BibleVerse> enumerable)
    {
        Regex regex = ScriptureRegex();

        Match match = regex.Match(search);

        if (!match.Success) return enumerable;

        string? book = null;
        if (match.Groups.TryGetValue("book", out Group? bookGroup) &&
            !string.IsNullOrWhiteSpace(bookGroup.Value))
            book = bookGroup.Value.ToLower();

        string? chapter = null;
        if (match.Groups.TryGetValue("chapter", out Group? chapterGroup) &&
            !string.IsNullOrWhiteSpace(chapterGroup.Value))
            chapter = chapterGroup.Value.ToLower();

        string? startVerse = null;
        if (match.Groups.TryGetValue("startVerse", out Group? startVerseGroup) &&
            !string.IsNullOrWhiteSpace(startVerseGroup.Value))
            startVerse = startVerseGroup.Value.ToLower();

        string? endVerse = null;
        if (match.Groups.TryGetValue("endVerse", out Group? endVerseGroup) &&
            !string.IsNullOrWhiteSpace(endVerseGroup.Value))
            endVerse = endVerseGroup.Value.ToLower();

        if (book != null)
        {
            enumerable = enumerable.Where(x => x.BookName.Contains(book, StringComparison.CurrentCultureIgnoreCase));
        }

        if (chapter != null)
        {
            enumerable = enumerable.Where(x =>
                x.ChapterNumber.ToString().Equals(chapter, StringComparison.CurrentCultureIgnoreCase));
        }

        if (startVerse != null)
        {
            if (endVerse == null)
            {
                enumerable = enumerable.Where(x =>
                    x.VerseNumber.ToString().Equals(startVerse, StringComparison.CurrentCultureIgnoreCase));
            }
            else if (int.TryParse(startVerse, out int start) && int.TryParse(endVerse, out int end))
            {
                enumerable = enumerable.Where(x => x.VerseNumber >= start && x.VerseNumber <= end);
            }
        }

        return enumerable;
    }


    [GeneratedRegex(ScriptureRegexString)]
    private static partial Regex ScriptureRegex();

    private const string ScriptureRegexString =
        @"\b(?<book>[1|2|3]{1}[ ]{1}[a-zA-Z]{2,11}|[I]{1,3}[ ]{1}[a-zA-Z]{2,11}|[a-zA-Z]{2,11})(?:\s(?<chapter>[0-9]{1,3})(?:(?:\:|\s)(?<startVerse>[0-9]{1,3})(?: {0,1}[\-,]{0,1} {0,1}(?<endVerse>[0-9]{1,3}))?)?)?\s?\b";
}