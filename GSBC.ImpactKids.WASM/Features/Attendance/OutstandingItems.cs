using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;

namespace GSBC.ImpactKids.WASM.Features.Attendance;

/// <summary>One thing a child is still holding, named well enough to go and find it.</summary>
public sealed record OutstandingItem(Guid PersonId, string PersonName, string Label);

/// <summary>
/// What has to be handed back before a child goes home.
/// <para>
/// Shared by the per-child sign out button and the household "sign out all" so the two can
/// never disagree about what is outstanding. They are the same question asked over one record
/// or over several, and a leader who is told a phone is outstanding by one control and not the
/// other has no way to tell which is lying.
/// </para>
/// </summary>
public static class OutstandingItems
{
    /// <summary>
    /// Outstanding means the item type says it must come back and the record does not say it
    /// has. An item type that has not loaded is treated as <em>not</em> outstanding: the point
    /// of this list is to name specific things, and "something, possibly" sends nobody anywhere.
    /// </summary>
    public static IReadOnlyList<OutstandingItem> For(
        IEnumerable<AttendanceRecord>        records,
        ImmutableList<AttendanceItemRecord>? itemRecords,
        ImmutableList<AttendanceItemType>?   itemTypes,
        Func<Guid, string>                   nameOf
    )
    {
        if (itemRecords == null || itemTypes == null)
            return [];

        Dictionary<Guid, AttendanceItemType> typesById = itemTypes.ToDictionary(x => x.Id);

        List<OutstandingItem> outstanding = [];

        foreach (AttendanceRecord record in records)
        {
            foreach (AttendanceItemRecord item in itemRecords.Where(x => x.AttendanceRecordId == record.Id))
            {
                if (item.ItemReturned == true)
                    continue;

                if (item.AttendanceItemTypeId == null ||
                    !typesById.TryGetValue(item.AttendanceItemTypeId.Value, out AttendanceItemType? type) ||
                    !type.RequiresReturning)
                    continue;

                outstanding.Add(new OutstandingItem(record.PersonId, nameOf(record.PersonId), type.Label));
            }
        }

        return outstanding;
    }
}
