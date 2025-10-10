namespace GSBC.ImpactKids.Grpc.Conversion;

public interface IConverter<in TIn, out TOut> : IConverter
{
    public TOut Convert(TIn input);
}

public interface IConverter
{
}