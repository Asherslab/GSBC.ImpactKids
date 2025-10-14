using System.Text.RegularExpressions;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.BibleServices;

public partial class BibleService(
    GsbcDbContext                        db,
    IConverter<DbBibleVerse, BibleVerse> converter
)
{
    
    [Authorize]
    public async Task<BasicReadMultipleResponse<BibleVerse>> ReadMultiple(
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
                if (match.Groups.TryGetValue("book", out Group? bookGroup) && !string.IsNullOrWhiteSpace(bookGroup.Value))
                    book = bookGroup.Value.ToLower();
                
                string? chapter = null;
                if (match.Groups.TryGetValue("chapter", out Group? chapterGroup) && !string.IsNullOrWhiteSpace(chapterGroup.Value))
                    chapter = chapterGroup.Value.ToLower();
                
                string? startVerse = null;
                if (match.Groups.TryGetValue("startVerse", out Group? startVerseGroup) && !string.IsNullOrWhiteSpace(startVerseGroup.Value))
                    startVerse = startVerseGroup.Value.ToLower();
                
                string? endVerse = null;
                if (match.Groups.TryGetValue("endVerse", out Group? endVerseGroup) && !string.IsNullOrWhiteSpace(endVerseGroup.Value))
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
        
        request.Pagination ??= new PaginationRequest();
        if (!request.Pagination.Disabled)
        {
            query = query
                .Skip(request.Pagination.Page * request.Pagination.PerPage)
                .Take(request.Pagination.PerPage);
        }

        List<DbBibleVerse> verses = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<BibleVerse>
        {
            Success = true,
            Entities = verses.Select(converter.Convert).ToList()
        };
    }
    
    private const string ScriptureRegexString =
        @"\b(?<book>[1|2|3]{1}[ ]{1}[a-zA-Z]{2,11}|[I]{1,3}[ ]{1}[a-zA-Z]{2,11}|[a-zA-Z]{2,11})(?:\s(?<chapter>[0-9]{1,3})(?:(?:\:|\s)(?<startVerse>[0-9]{1,3})(?: {0,1}[\-,]{0,1} {0,1}(?<endVerse>[0-9]{1,3}))?)?)?\s?\b";

    [GeneratedRegex(ScriptureRegexString)]
    private static partial Regex ScriptureRegex();
}