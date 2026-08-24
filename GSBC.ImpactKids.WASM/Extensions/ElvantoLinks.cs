namespace GSBC.ImpactKids.WASM.Extensions;

public static class ElvantoLinks
{
    public static string GetPersonUrl(string elvantoId) =>
        $"https://ministry.baptist.com.au/admin/people/person/?id={elvantoId}";
}
