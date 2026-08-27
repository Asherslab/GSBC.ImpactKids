namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

/// <summary>
/// What a request does to Elvanto. Creates and updates are separated because they are enabled
/// separately: a first controlled write wants creates on and updates off, and the two carry very
/// different risk - an unwanted update edits one record, an unwanted create adds a person who then
/// has to be found and removed by hand.
/// </summary>
public enum ElvantoMutation
{
    None,
    Create,
    Update
}

public interface IRequestMessage
{
    public static abstract Uri RequestUri { get; }

    /// <summary>
    /// What this request does to Elvanto. Abstract rather than defaulted so a new request type
    /// cannot be added without deciding, and cannot default its way past the write guards in
    /// <c>ElvantoService.SendMessage</c>.
    /// </summary>
    public static abstract ElvantoMutation Mutation { get; }
}
