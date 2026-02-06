using System.Text.RegularExpressions;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.BibleServices;

public partial class BibleService(
    GsbcDbContext                        db,
    IConverter<DbBibleVerse, BibleVerse> converter
)
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<BibleVerse>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbBibleVerse> query = db.BibleVerses;

        if (request.SearchString != null)
        {
            Regex regex = ScriptureRegex();

            Match match = regex.Match(request.SearchString);

            if (match.Success)
            {
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
                    query = query.Where(x => x.BookName.ToLower() == book);
                }

                if (chapter != null)
                {
                    query = query.Where(x => x.ChapterNumber.ToString().ToLower() == chapter);
                }

                if (startVerse != null)
                {
                    if (endVerse == null)
                    {
                        query = query.Where(x => x.VerseNumber.ToString().ToLower() == startVerse);
                    }
                    else if (int.TryParse(startVerse, out int start) && int.TryParse(endVerse, out int end))
                    {
                        query = query.Where(x => x.VerseNumber >= start && x.VerseNumber <= end);
                    }
                }
            }
        }

        query = query
            .OrderBy(x => x.BookNumber)
            .ThenBy(x => x.ChapterNumber)
            .ThenBy(x => x.VerseNumber);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<BibleVerse> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }

    private const string ScriptureRegexString =
        @"\b(?<book>[1|2|3]{1}[ ]{1}[a-zA-Z]{2,11}|[I]{1,3}[ ]{1}[a-zA-Z]{2,11}|[a-zA-Z]{2,11})(?:\s(?<chapter>[0-9]{1,3})(?:(?:\:|\s)(?<startVerse>[0-9]{1,3})(?: {0,1}[\-,]{0,1} {0,1}(?<endVerse>[0-9]{1,3}))?)?)?\s?\b";

    [GeneratedRegex(ScriptureRegexString)]
    private static partial Regex ScriptureRegex();
}