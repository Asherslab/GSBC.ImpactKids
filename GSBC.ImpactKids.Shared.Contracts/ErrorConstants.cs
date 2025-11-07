namespace GSBC.ImpactKids.Shared.Contracts;

public static class ErrorConstants
{
    public const string PermissionDenied  = "Permission Denied";
    public const string ExceptionOccurred = "An Unexpected error occurred. Please try again later";

    public const string EventStreamIdNotFound =
        "Event service couldn't find stream id running. Might have disconnected?";

    public const string EventStreamNotRunning = "Event stream not running";

    public const string FailedToRetrieveServices = "Failed to retrieve elvanto services";

    public const string UserNotFound         = "User Not Found";
    public const string UserCannotToggleSelf = "User Cannot Enable Their Own User";
    public const string UserCannotDeleteSelf = "User Cannot Delete Their Own User";

    public const string PersonNotFound      = "Person Not Found";
    public const string PersonFirstNameNull = "Person First Name Must Be Set";
    public const string PersonLastNameNull  = "Person Last Name Must Be Set";

    public const string SchoolGradeNotFound             = "School Grade Not Found";
    public const string MediaConsentNotFound            = "Media Consent Value Not Found";
    public const string MedicalTypeNotFound             = "Medical Note Type Not Found";
    public const string AllergenNotFound                = "Allergen Not Found";
    public const string MedicalNoteNotFound             = "Medical Note Not Found";
    public const string AllergyNotFound                 = "Allergy Not Found";
    public const string MedicalNotesMustHaveTypeOrNotes = "Medical Notes Must Have A Type or Notes Text";
    public const string AllergiesMustHaveTypeOrNotes    = "Allergies Must Have A Type or Notes Text";

    public const string SchoolTermNotFound      = "School Term Not Found";
    public const string SchoolTermNameNull      = "School Term Name Must Be Set";
    public const string SchoolTermStartDateNull = "School Term Start Date Must Be Set";
    public const string SchoolTermEndDateNull   = "School Term End Date Must Be Set";

    public const string ServiceNotFound       = "Service Not Found";
    public const string ServiceSchoolTermNull = "Service School Term Must Be Set";
    public const string ServiceDateNull       = "Service Date Must Be Set";

    public const string DollarStoreEntryNotFound = "Dollar Store Entry Not Found";
    public const string DollarStoreServiceExists = "Service can only have one Dollar Store Entry";

    public const string MemoryVerseListNotFound = "Memory Verse List Not Found";
    public const string MemoryVerseListNameNull = "Memory Verse List Name Must Be Set";

    public const string MemoryVerseNotFound          = "Memory Verse Not Found";
    public const string MemoryVerseReferenceNameNull = "Memory Verse Reference Name Must Be Set";
    public const string MemoryVerseVerseNull         = "Memory Verse Text Must Be Set";

    public const string BibleVerseNotFound = "Bible Verse Not Found";

    public const string MemorisationEntryNotFound = "Memorisation Entry Not Found";
}