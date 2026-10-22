using System.Text.Json;
using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Tests.Elvanto;

/// <summary>
/// On 2026-10-22 prod's <c>people/getAll</c> started answering with <c>"family_id": 4671</c> - a bare
/// number - where it had always sent <c>"4671"</c>. <see cref="ElvantoPerson.FamilyId"/> is a string,
/// so the deserializer threw on <c>person[0]</c>, the transport's catch-all returned default off a
/// clean HTTP 200, and every sync failed with "page 1 after 3 attempts, 0 people fetched".
///
/// Same failure shape as <see cref="GetPersonInfoShapeTests"/>, so the same remedy: assert the wire
/// shape rather than assume it. Both forms are pinned, since Elvanto has now sent both.
/// </summary>
public class GetAllShapeTests
{
    /// <summary>Mirrors <c>ElvantoService._jsonOptions</c>; see <see cref="GetPersonInfoShapeTests"/>.</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static string Page(string familyId) => $$"""
        {
          "generated_in": "0.05",
          "status": "ok",
          "people": {
            "on_this_page": 1,
            "page": 1,
            "per_page": "1000",
            "total": 1,
            "person": [
              {
                "id": "145b626f-804f-46c9-ae32-339e5e640f24",
                "firstname": "Testy",
                "lastname": "Mctestface",
                "family_id": {{familyId}},
                "family_relationship": "Primary Contact"
              }
            ]
          }
        }
        """;

    [Theory]
    [InlineData("4671")]
    [InlineData("\"4671\"")]
    public void FamilyIdReadsAsTheSameStringEitherWay(string familyId)
    {
        PeopleResponse? resp = JsonSerializer.Deserialize<PeopleResponse>(Page(familyId), Options);

        ElvantoPerson person = Assert.Single(resp!.People!.Person!);
        Assert.Equal("4671", person.FamilyId);
    }

    [Fact]
    public void AMissingFamilyStaysNull()
    {
        PeopleResponse? resp = JsonSerializer.Deserialize<PeopleResponse>(Page("null"), Options);

        Assert.Null(Assert.Single(resp!.People!.Person!).FamilyId);
    }
}
