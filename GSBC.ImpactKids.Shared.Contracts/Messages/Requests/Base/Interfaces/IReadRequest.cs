namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

public interface IReadRequest
{
    public string Id { get; set; }
}

public abstract class ReadRequestBase : IReadRequest
{
    public abstract string Id { get; set; }

    public Guid Guid
    {
        get => Guid.Parse(Id);
        set => Id = value.ToString();
    }
}