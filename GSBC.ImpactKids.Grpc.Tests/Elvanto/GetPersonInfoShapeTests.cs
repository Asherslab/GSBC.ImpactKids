using System.Text.Json;
using System.Text.Json.Serialization;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Tests.Elvanto;

/// <summary>
/// Elvanto's <c>people/getInfo</c> answers with <c>"person": [ { ... } ]</c> — an array, even for a
/// single id. <see cref="ElvantoGetPersonInfoResponse.Person"/> declared a single object, so every
/// call threw inside the deserializer, the transport's catch-all returned default, and the caller
/// read null as "no such person" off a clean HTTP 200.
///
/// Nothing failed loudly, which is the whole reason it survived: it disabled the family read-back on
/// <c>people/edit</c> (the only source of a newly minted household's id, so households fragmented
/// one per person) and it silently emptied every person- and family-scoped sync before those were
/// removed.
///
/// These tests exist so the shape is asserted rather than assumed. The payloads below are the real
/// wire shape, confirmed against the live Elvanto account on 2026-08-27, with invented values.
/// </summary>
public class GetPersonInfoShapeTests
{
    /// <summary>
    /// The same options the transport deserializes with — <c>ElvantoService._jsonOptions</c>. Copied
    /// rather than shared because it is private; if that ever gains a converter, this needs it too,
    /// or these tests stop describing what happens on the wire.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string RealShape = """
        {
          "generated_in": "0.05",
          "status": "ok",
          "person": [
            {
              "id": "145b626f-804f-46c9-ae32-339e5e640f24",
              "firstname": "Testy",
              "lastname": "Mctestface",
              "family_id": "4671",
              "family_relationship": "Primary Contact"
            }
          ]
        }
        """;

    [Fact]
    public void TheShapeElvantoActuallySendsDeserialises()
    {
        ElvantoGetPersonInfoResponse? resp =
            JsonSerializer.Deserialize<ElvantoGetPersonInfoResponse>(RealShape, Options);

        Assert.NotNull(resp);
        Assert.NotNull(resp.Person);
        Assert.Single(resp.Person);
    }

    /// <summary>
    /// The two fields the read-back callers consume. <c>family_id</c> is the one the defect cost:
    /// <c>people/edit</c> never reports the family it created, so this is the only place that id can
    /// come from, and a null meant no <c>ElvantoFamilyLinks</c> row and a fresh household for the
    /// next sibling.
    /// </summary>
    [Fact]
    public void TheReadBackFieldsSurviveTheArray()
    {
        ElvantoPerson? person =
            JsonSerializer.Deserialize<ElvantoGetPersonInfoResponse>(RealShape, Options)
                ?.Person?.FirstOrDefault();

        Assert.NotNull(person);
        Assert.Equal("145b626f-804f-46c9-ae32-339e5e640f24", person.Id);
        Assert.Equal("4671", person.FamilyId);
    }

    /// <summary>
    /// The defect itself, pinned. <see cref="OldShape"/> mirrors the declaration this replaced, so
    /// this asserts what actually went wrong rather than describing it in a comment: the real payload
    /// does not merely come back empty against a single-object declaration, it <b>throws</b>. The
    /// transport catches that and returns default, which is how a parse failure became "no such
    /// person".
    ///
    /// Without this, every other test here would pass just as happily against the broken model.
    /// </summary>
    [Fact]
    public void TheOldSingleObjectDeclarationThrowsOnTheRealPayload()
    {
        JsonException ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<OldShape>(RealShape, Options));

        Assert.Contains("$.person", ex.Message);
    }

    /// <summary>What <see cref="ElvantoGetPersonInfoResponse"/> used to say. Kept only for the test above.</summary>
    private sealed class OldShape
    {
        [JsonPropertyName("person")]
        public ElvantoPerson? Person { get; set; }
    }

    /// <summary>
    /// An empty array is Elvanto having no such person, and must read as null rather than throw. This
    /// is the case the old declaration conflated with a parse failure, and the one callers
    /// legitimately need to tell apart from it.
    /// </summary>
    [Fact]
    public void NoSuchPersonIsNullRatherThanAThrow()
    {
        const string empty = """{"status":"ok","person":[]}""";

        ElvantoGetPersonInfoResponse? resp =
            JsonSerializer.Deserialize<ElvantoGetPersonInfoResponse>(empty, Options);

        Assert.NotNull(resp);
        Assert.Empty(resp.Person!);
        Assert.Null(resp.Person!.FirstOrDefault());
    }
}
