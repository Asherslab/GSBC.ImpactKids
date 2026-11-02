using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay;

/// <summary>An entry at the top of the Analytics menu; a folder carries its own entries in <see cref="Children"/>.</summary>
public record MetabaseMenuEntry(MetabaseEmbed Embed, ImmutableList<MetabaseEmbed> Children);

public static class MetabaseMenu
{
    /// <summary>
    /// The menu as the admin page arranges it. An entry whose folder has gone stays visible at the top
    /// rather than vanishing - the server never leaves one, but a half-refreshed list could.
    /// </summary>
    public static ImmutableList<MetabaseMenuEntry> Build(IEnumerable<MetabaseEmbed> embeds)
    {
        List<MetabaseEmbed> ordered = embeds.OrderBy(x => x.SortOrder).ThenBy(x => x.Label).ToList();
        HashSet<Guid>       folders = ordered.Where(x => x.IsFolder).Select(x => x.Id).ToHashSet();

        return ordered
            .Where(x => x.ParentId == null || !folders.Contains(x.ParentId.Value))
            .Select(x => new MetabaseMenuEntry(
                x,
                x.IsFolder ? ordered.Where(c => c.ParentId == x.Id).ToImmutableList() : []
            ))
            .ToImmutableList();
    }

    /// <summary>The menu as a leader sees it: a folder with nothing in it is left out.</summary>
    public static ImmutableList<MetabaseMenuEntry> ForNav(IEnumerable<MetabaseEmbed> embeds) =>
        Build(embeds).Where(x => !x.Embed.IsFolder || !x.Children.IsEmpty).ToImmutableList();

    /// <summary>
    /// A drag applied to a local copy, the same way <c>MetabaseService.Move</c> applies it, so the page
    /// shows the drop at once instead of snapping back until the store refreshes.
    /// </summary>
    public static ImmutableList<MetabaseEmbed> Move(
        IEnumerable<MetabaseEmbed> embeds,
        Guid                       id,
        Guid?                      parentId,
        int                        position
    )
    {
        List<MetabaseEmbed> all   = embeds.ToList();
        MetabaseEmbed?      moved = all.FirstOrDefault(x => x.Id == id);

        if (moved == null || (moved.IsFolder && parentId != null))
            return all.ToImmutableList();

        List<MetabaseEmbed> Siblings(Guid? parent) => all
            .Where(x => x.ParentId == parent && x.Id != id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Label)
            .ToList();

        List<MetabaseEmbed> from = Siblings(moved.ParentId);
        List<MetabaseEmbed> to   = parentId == moved.ParentId ? from : Siblings(parentId);
        to.Insert(Math.Clamp(position, 0, to.Count), moved with { ParentId = parentId });

        Dictionary<Guid, MetabaseEmbed> renumbered = [];

        foreach (List<MetabaseEmbed> list in new[] { from, to }.Distinct())
        {
            for (int i = 0; i < list.Count; i++)
                renumbered[list[i].Id] = list[i] with { SortOrder = i };
        }

        return all.Select(x => renumbered.GetValueOrDefault(x.Id, x)).ToImmutableList();
    }

    public static string Href(MetabaseEmbed embed) => $"/Analytics/{Uri.EscapeDataString(embed.Label)}";

    public static string Icon(MetabaseEmbed embed) => embed.Type switch
    {
        MetabaseEmbedType.Folder   => MudBlazor.Icons.Material.Filled.Folder,
        MetabaseEmbedType.Question => MudBlazor.Icons.Material.Filled.TableChart,
        _                          => MudBlazor.Icons.Material.Filled.AreaChart
    };
}
