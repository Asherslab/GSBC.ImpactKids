namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Interfaces;

public interface IRequestMessage
{
    public static abstract Uri RequestUri { get; }

    /// <summary>
    /// True if this request changes data in Elvanto. Abstract rather than defaulted so a new
    /// request type cannot be added without deciding, and cannot default its way past the
    /// write guard in <c>ElvantoService.SendMessage</c>.
    /// </summary>
    public static abstract bool IsMutation { get; }
}