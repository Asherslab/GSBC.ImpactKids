namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

/// <summary>
/// Which school grades belong to which part of the night. Kept here rather than beside
/// each page so the phone, the tally and the server never disagree about who is in the room.
/// Labels are the ones Elvanto sends, so they are matched by string, not by order number.
/// </summary>
public static class SchoolGradeTiers
{
    /// <summary>
    /// A child this old is in the program whatever their grade label says. The junior program
    /// takes five year olds, and a five year old is not always recorded as Prep - some are
    /// still sitting in Kindergarten or Pre-school in Elvanto.
    /// </summary>
    public const int MinimumProgramAge = 5;

    /// <summary>Prep through grade 6 - the whole program, junior and primary together.</summary>
    public static readonly string[] Program =
    [
        "Prep",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6"
    ];

    /// <summary>
    /// Below Prep. Not the program by grade alone - but a child here who has turned
    /// <see cref="MinimumProgramAge"/> is, which is what <see cref="IsInProgram"/> is for.
    /// </summary>
    public static readonly string[] EarlyYears =
    [
        "Nursery/Pre-school",
        "Kindergarten"
    ];

    public static readonly string[] HighSchool =
    [
        "7",
        "8",
        "9",
        "10",
        "11",
        "12"
    ];

    /// <summary>Every grade a memory verse can be logged against.</summary>
    public static readonly string[] EarlyYearsAndProgram = [..EarlyYears, ..Program];

    /// <summary>
    /// Why this person looks out of place at sign in, or null when they belong. A sentence
    /// rather than a flag, because the desk needs to know which way they fall out - too
    /// young, too old, or simply never recorded - to decide what to do about it.
    /// </summary>
    public static string? OutOfProgramWarning(Person person, IEnumerable<SchoolGrade> grades)
    {
        SchoolGrade? grade = grades.FirstOrDefault(x => x.Id == person.SchoolGradeId);
        int?         age   = person.GetAge();

        if (grade == null)
        {
            return age == null
                ? "No school grade or date of birth recorded, so there is no way to tell which program they belong in."
                : $"No school grade recorded. They are {age}, and the program runs from Prep, or age {MinimumProgramAge}, to grade 6.";
        }

        if (Program.Contains(grade.Label))
            return null;

        if (EarlyYears.Contains(grade.Label))
        {
            if (age >= MinimumProgramAge)
                return null;

            return age == null
                ? $"Recorded as {grade.Label} with no date of birth. The juniors start at Prep, or age {MinimumProgramAge}."
                : $"Recorded as {grade.Label} and aged {age}. The juniors start at Prep, or age {MinimumProgramAge}.";
        }

        return $"Recorded in grade {grade.Label}, past the end of the program at grade 6.";
    }

    /// <summary>
    /// True when the person is in the program: Prep to grade 6 by label, or below Prep but
    /// old enough to have joined the juniors. A missing date of birth is not old enough -
    /// an unknown age is left in the early years tier rather than guessed upward.
    /// </summary>
    public static bool IsInProgram(Person person, IEnumerable<SchoolGrade> grades)
    {
        SchoolGrade? grade = grades.FirstOrDefault(x => x.Id == person.SchoolGradeId);

        if (grade == null)
            return false;

        return Program.Contains(grade.Label) ||
               (EarlyYears.Contains(grade.Label) && person.GetAge() >= MinimumProgramAge);
    }
}
