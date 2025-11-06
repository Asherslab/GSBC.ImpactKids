using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemorisationEntryRequest : ReadRequestBase
{
    public override string              Id   { get; set; } = null!;
    
    public DeltaUpdate<bool> VerseRecited         { get; set; } = new();
    public DeltaUpdate<bool> FiveDollaryDoosGiven { get; set; } = new();
    public DeltaUpdate<bool> OneDollaryDooGiven   { get; set; } = new();
}